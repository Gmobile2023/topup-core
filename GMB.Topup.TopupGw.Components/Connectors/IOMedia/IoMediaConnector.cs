using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.Shared.Utils;
using GMB.Topup.TopupGw.Contacts.ApiRequests;
using GMB.Topup.TopupGw.Contacts.Dtos;
using GMB.Topup.TopupGw.Contacts.Enums;
using GMB.Topup.TopupGw.Domains.BusinessServices;
using GMB.Topup.TopupGw.Domains.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.WireProtocol.Messages;
using ServiceStack;
using ServiceStack.Script;

namespace GMB.Topup.TopupGw.Components.Connectors.IOMedia;

public class IoMediaConnector : IGatewayConnector
{
    public const string TRANS_TYPE_TOPUP = "TU";
    public const string TRANS_TYPE_BUYCARD = "BC";
    public const string TRANS_TYPE_PAYBILL = "VB";

    private readonly ILogger<IoMediaConnector> _logger; // = LogManager.GetLogger("ZotaConnector");
    private readonly ITopupGatewayService _topupGatewayService;

    public IoMediaConnector(ITopupGatewayService topupGatewayService, ILogger<IoMediaConnector> logger)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        using (_logger.BeginScope(topupRequestLog.TransCode))
        {
            _logger.LogInformation("IoMediaConnector topup request: " + topupRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();

            try
            {
                if (!_topupGatewayService.ValidConnector(ProviderConst.IOMEDIA, providerInfo.ProviderCode))
                {
                    _logger.LogError(
                        $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-IoMediaConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                    };
                }

                var client = new JsonHttpClient(providerInfo.ApiUrl)
                {
                    HttpClient = new HttpClient
                    {
                        Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                        BaseAddress = new Uri(providerInfo.ApiUrl)
                    }
                };

                var isPayBill = topupRequestLog.ProductCode.StartsWith("VMS_BILL")
                                || topupRequestLog.ProductCode.StartsWith("VNA_BILL")
                                || topupRequestLog.ProductCode.StartsWith("VTE_BILL");

                IoRequest request = null;
                string function =
                    isPayBill ? "/IPayService/rest/partner/payBill" : "/IPayService/rest/partner/directTopup";
                if (isPayBill)
                {
                    var providerService =
                        providerInfo.ProviderServices.Find(p => p.ProductCode == topupRequestLog.ProductCode);
                    var serviceCode = string.Empty;
                    if (providerService != null)
                        serviceCode = providerService.ServiceCode;

                    request = new IoRequest
                    {
                        PartnerCode = providerInfo.Username,
                        ProductCode = serviceCode,
                        PartnerTransId = topupRequestLog.TransCode,
                        BillingCode = topupRequestLog.ReceiverInfo,
                        PaidAmount = (long)topupRequestLog.TransAmount
                    };
                }
                else
                {
                    request = new IoRequest()
                    {
                        PartnerCode = providerInfo.Username,
                        PartnerTransId = topupRequestLog.TransCode,
                        TopupAmount = topupRequestLog.TransAmount,
                        MobileNo = topupRequestLog.ReceiverInfo,
                        TelcoCode = topupRequestLog.Vendor
                    };

                    if (topupRequestLog.ServiceCode != "TOPUP_DATA")
                    {
                        if (request.TelcoCode == "VTE")
                            request.TelcoCode = "VT";
                        if (request.TelcoCode == "VNA")
                            request.TelcoCode = "VP";
                        if (request.TelcoCode == "GMOBILE")
                            request.TelcoCode = "BL";
                        if (request.TelcoCode == "VNM")
                            request.TelcoCode = "VM";
                        if (request.TelcoCode == "VMS")
                            request.TelcoCode = "MB";
                    }
                    else
                    {
                        if (request.TelcoCode == "VMS")
                            request.TelcoCode = "DV";
                    }
                }

                responseMessage.TransCodeProvider = topupRequestLog.TransCode;

                try
                {
                    if (isPayBill)
                    {
                        request.Sign = Sign(
                            string.Join("", request.PartnerCode, request.PartnerTransId, request.ProductCode,
                                request.BillingCode, request.PaidAmount),
                            "./" + providerInfo.PrivateKeyFile);
                    }
                    else
                    {
                        request.Sign = Sign(string.Join("", request.PartnerCode, request.PartnerTransId,
                            request.TelcoCode,
                            request.MobileNo, request.TopupAmount), "./" + providerInfo.PrivateKeyFile);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError("Error sign data: " + e.Message);
                    topupRequestLog.Status = TransRequestStatus.Fail;
                    await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
                }

                IoResponse result = null;
                using (_logger.BeginScope("Send request to provider IOMEDIA"))
                {
                    _logger.LogInformation("IoMediaConnector send: " + request.ToJson());
                    //The operation has timed out.
                    try
                    {
                        result = await client.PostAsync<IoResponse>(function, request);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"{request.PartnerTransId} IoMediaConnector exception: " + ex.Message);
                        result = new IoResponse
                        {
                            ResCode = "501102",
                            ResMessage = ex.Message
                        };
                    }
                }

                if (result != null)
                {
                    topupRequestLog.ModifiedDate = DateTime.Now;
                    topupRequestLog.ResponseInfo = result.ToJson();
                    _logger.LogInformation(
                        $"{topupRequestLog.ProviderCode}-{request.PartnerTransId} Topup return: {topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");

                    responseMessage.ProviderResponseCode = result?.ResCode;
                    responseMessage.ProviderResponseMessage = result?.ResMessage;

                    if (result.ResCode == "00")
                    {
                        topupRequestLog.Status = TransRequestStatus.Success;
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        responseMessage.ReceiverType = result.MobileType;
                    }
                    else if (new[] { "08", "501102" }.Contains(result.ResCode))
                    {
                        topupRequestLog.Status = TransRequestStatus.Timeout;
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage =
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    }
                    else
                    {
                        var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IOMEDIA,
                            result.ResCode, topupRequestLog.TransCode);
                        topupRequestLog.Status = TransRequestStatus.Fail;
                        responseMessage.ResponseCode =
                            reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result?.ResMessage;
                    }

                    responseMessage.ProviderResponseCode = result?.ResCode;
                    responseMessage.ProviderResponseMessage = result?.ResMessage;
                }
                else
                {
                    _logger.LogInformation("Error send request");
                    responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
                    topupRequestLog.Status = TransRequestStatus.Fail;
                }

                await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
            }
            catch (Exception e)
            {
                _logger.LogError($"{topupRequestLog.TransCode} -{topupRequestLog.TransRef} Topup error {e}");
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }
            return responseMessage;
        }
    }

    public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
    {
        try
        {
            _logger.LogInformation(
                $"IoMediaConnector check request: {transCodeToCheck}-{transCode}-{providerCode}-{serviceCode}");
            var responseMessage = new MessageResponseBase();
            var transType =
                serviceCode == ServiceCodes.TOPUP ? "TU" :
                serviceCode == ServiceCodes.PIN_CODE ? "BC" :
                serviceCode == ServiceCodes.PAY_BILL ? "VB" : "";
            if (providerInfo == null)
                providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null ||
                !_topupGatewayService.ValidConnector(ProviderConst.IOMEDIA, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{transCode}-{providerCode}-{providerCode}-IoMediaConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            var client = new JsonHttpClient(providerInfo.ApiUrl)
            {
                HttpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                    BaseAddress = new Uri(providerInfo.ApiUrl)
                }
            };

            var request = new IoRequest
            {
                PartnerCode = providerInfo.Username,
                TransType = transType,
                PartnerTransId = transCodeToCheck
            };
            var sign = Sign(string.Join("", request.PartnerCode, request.PartnerTransId, request.TransType),
                "./" + providerInfo.PrivateKeyFile);

            request.Sign = sign;
            _logger.LogInformation(
                $"{providerCode}-{transCodeToCheck} IoMediaConnector check send: " + request.ToJson());
            IoResponse result = null;
            try
            {
                result = await client.PostAsync<IoResponse>("/IPayService/rest/partner/checkTransaction", request);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{transCodeToCheck} IoMediaConnector check exception: " + ex.Message);
                result = new IoResponse
                {
                    ResCode = "501102", //Tự quy định mã này cho trường hợp timeout.
                    ResMessage = ex.Message
                };
            }

            if (result != null)
            {
                _logger.LogInformation(
                    $"{providerCode}-{transCodeToCheck} IoMediaConnector check return:{transCode}-{transCodeToCheck} => {result.ToJson()}");
                //responseMessage.ExtraInfo = string.Join("|", result.ResCode, result.ResMessage);
                //var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("IOMEDIA", result.ResCode, transCode);
                if (result.ResCode == "00")
                {
                    if (result.TransStatus == 0)
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        responseMessage.Payload = "Giao dịch thành công";
                        responseMessage.ReceiverType = result.MobileType;
                        if ((serviceCode ?? "").StartsWith("PIN"))
                        {
                            var retrieveResponse = await GetRetrieveCardInfo(providerInfo, transCodeToCheck);
                            responseMessage.Payload = retrieveResponse;
                        }
                    }
                    else if (new[] { 1 }.Contains(result.TransStatus))
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage =
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    }
                    else
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
                    }
                }
                else
                {
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }

                responseMessage.ProviderResponseCode = result?.ResCode;
                responseMessage.ProviderResponseMessage = result?.ResMessage;
            }
            else
            {
                _logger.LogInformation($"{transCodeToCheck} Error send request");
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }

            return responseMessage;
        }
        catch (Exception ex)
        {
            return new MessageResponseBase
            {
                ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                Exception = ex.Message
            };
        }
    }

    public async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.Log(LogLevel.Information,
            $"{payBillRequestLog.TransCode} IoMediaConnector query request: " + payBillRequestLog.ToJson());
        var responseMessage = new NewMessageResponseBase<InvoiceResultDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Truy vấn thông tin không thành công")
        };
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.IOMEDIA, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-IoMediaConnector ProviderConnector not valid");
            responseMessage.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ");
            return responseMessage;
        }

        var client = new JsonHttpClient(providerInfo.ApiUrl)
        {
            HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                BaseAddress = new Uri(providerInfo.ApiUrl)
            }
        };

        var providerService =
            providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
        var serviceCode = string.Empty;
        if (providerService != null)
            serviceCode = providerService.ServiceCode;
        else
            _logger.LogWarning(
                $"{payBillRequestLog.TransCode} ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");

        var request = new IoRequest
        {
            PartnerCode = providerInfo.Username,
            BillingCode = payBillRequestLog.ReceiverInfo,
            ProductCode = serviceCode
        };
        _logger.Log(LogLevel.Information,
            $"{payBillRequestLog.TransCode}-IoMediaConnector query request: " + request.ToJson());
        var sign = Sign(string.Join("", request.PartnerCode, request.ProductCode, request.BillingCode),
            "./" + providerInfo.PrivateKeyFile);

        request.Sign = sign;

        _logger.LogInformation("IoMediaConnector query send: " + request.ToJson());

        IoResponse result = null;
        try
        {
            result = await client.PostAsync<IoResponse>("/IPayService/rest/partner/queryBill", request);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{payBillRequestLog.TransCode}Query exception: " + ex.Message);
            result = new IoResponse
            {
                ResCode = "501102", //Tự quy định mã này cho trường hợp timeout.
                ResMessage = ex.Message
            };
        }

        if (result != null)
        {
            _logger.LogInformation(
                $"{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode} IoMediaConnector Query return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");

            if (result.ResCode == "00")
            {
                responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Success;
                responseMessage.ResponseStatus.Message = "Giao dịch thành công";
                responseMessage.Results = new InvoiceResultDto
                {
                    Amount = result.Amount,
                    CustomerReference = request.BillingCode,
                    CustomerName = result.BillingName
                };
            }
            else if (new[] { "08", "501102" }.Contains(result.ResCode))
            {
                responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseStatus.Message =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }
            else
            {
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("IOMEDIA", result.ResCode,
                    payBillRequestLog.TransCode);

                responseMessage.ResponseStatus.ErrorCode =
                    reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseStatus.Message =
                    reResult != null ? reResult.ResponseName : result.ResMessage;
            }
        }
        else
        {
            _logger.LogInformation($"{payBillRequestLog.TransCode} Error send request");
            responseMessage.ResponseStatus.Message = "Lỗi kết nối nhà cung cấp";
        }

        return responseMessage;
    }

    public async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        _logger.Log(LogLevel.Information,
            $"{cardRequestLog.TransCode} Get card request: " + cardRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.IOMEDIA, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{cardRequestLog.ProviderCode}-IoMediaConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var client = new JsonHttpClient(providerInfo.ApiUrl)
        {
            HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                BaseAddress = new Uri(providerInfo.ApiUrl)
            }
        };

        var request = new IoRequest
        {
            PartnerCode = providerInfo.Username, // "0866697567",
            PartnerTransId = cardRequestLog.TransCode,
            Quantity = cardRequestLog.Quantity,
            Reciever = cardRequestLog.ReceiverInfo
        };

        responseMessage.TransCodeProvider = cardRequestLog.TransCode;
        responseMessage.ExtraInfo = request.PartnerTransId;
        var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == cardRequestLog.ProductCode);

        request.ProductCode = providerService.ServiceCode;

        var sign = Sign(
            string.Join("", request.PartnerCode, request.PartnerTransId, request.ProductCode, request.Quantity),
            "./" + providerInfo.PrivateKeyFile);

        request.Sign = sign;

        _logger.LogInformation($"{cardRequestLog.TransCode} Card object send: " + request.ToJson());

        IoResponse result = null;
        try
        {
            result = await client.PostAsync<IoResponse>("/IPayService/rest/partner/buyCard", request);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{cardRequestLog.TransCode} Get Cards exception: " + ex.Message);
            result = new IoResponse
            {
                ResCode = "501102", //Tự quy định mã này cho trường hợp timeout.
                ResMessage = ex.Message
            };
        }

        if (result != null)
        {
            cardRequestLog.ModifiedDate = DateTime.Now;
            cardRequestLog.ResponseInfo = result.ToJson();
            _logger.Log(LogLevel.Information,
                $"{cardRequestLog.ProviderCode}-{cardRequestLog.TransCode} Card return: {cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{result.ToJson()}");

            if (result.ResCode == "00")
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                cardRequestLog.Status = TransRequestStatus.Success;
                try
                {
                    var cardList = result.CardList.Select(card => new CardRequestResponseDto
                        {
                            CardType = cardRequestLog.Vendor,
                            CardValue = (int.Parse(cardRequestLog.ProductCode.Split('_')[2]) * 1000).ToString(),
                            CardCode = card.Pincode,
                            Serial = card.Serial,
                            ExpireDate = TimeStampToDate(card.Expiredate).ToString("dd/MM/yyyy"),
                            ExpiredDate = TimeStampToDate(card.Expiredate)
                        })
                        .ToList();

                    cardList = GenDecryptListCode(providerInfo.PrivateKeyFile, cardList);
                    responseMessage.Payload = cardList;
                }
                catch (Exception e)
                {
                    _logger.LogError($"{cardRequestLog.TransCode} Error parsing cards: " + e.Message);
                }
            }
            else if (new[] { "08", "501102" }.Contains(result.ResCode))
            {
                cardRequestLog.Status = TransRequestStatus.Timeout;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }
            else
            {
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IOMEDIA,
                    result.ResCode, cardRequestLog.TransCode);
                cardRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.ResMessage;
            }

            responseMessage.ProviderResponseCode = result?.ResCode;
            responseMessage.ProviderResponseMessage = result?.ResMessage;
        }
        else
        {
            _logger.LogInformation($"{cardRequestLog.TransCode} Error send request");
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            cardRequestLog.Status = TransRequestStatus.Fail;
        }

        await _topupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);


        return responseMessage;
    }

    public async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        _logger.Log(LogLevel.Information, "Get balance request: " + transCode);
        var responseMessage = new MessageResponseBase();

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.IOMEDIA, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{providerCode}-{transCode}-{providerInfo.ProviderCode}-IoMediaConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var client = new JsonHttpClient(providerInfo.ApiUrl)
        {
            HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                BaseAddress = new Uri(providerInfo.ApiUrl)
            }
        };

        var request = new IoRequest
        {
            PartnerCode = providerInfo.Username
        };
        var sign = Sign(providerInfo.Username, providerInfo.PrivateKeyFile);

        request.Sign = sign;
        _logger.LogInformation($"ProviderCode= {providerCode}|Balance_object_send: " + request.ToJson());

        IoResponse result = null;
        try
        {
            result = await client.PostAsync<IoResponse>("/IPayService/rest/partner/checkBalance", request);
        }
        catch (Exception ex)
        {
            _logger.LogError($"ProviderCode= {providerCode}|Balance_exception: " + ex.Message);
            result = new IoResponse
            {
                ResCode = "501102", //Tự quy định mã này cho trường hợp timeout.
                ResMessage = ex.Message
            };
        }

        if (result != null)
        {
            _logger.Log(LogLevel.Information, $"Balance return: {providerCode}-{transCode}-{result.ToJson()}");

            if (result.ResCode == "00")
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                responseMessage.Payload = result.CurrentBalance;
            }
            else if (new[] { "4501", "501102" }.Contains(result.ResCode))
            {
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }
            else
            {
                var reResult =
                    await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IOMEDIA, result.ResCode,
                        transCode);
                responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.ResMessage;
            }
        }
        else
        {
            _logger.LogInformation("Error send request");
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
        }

        return responseMessage;
    }

    public Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
    {
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.Log(LogLevel.Information, "Get Paybill request: " + payBillRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.IOMEDIA, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-IoMediaConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var client = new JsonHttpClient(providerInfo.ApiUrl)
        {
            HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                BaseAddress = new Uri(providerInfo.ApiUrl)
            }
        }; //("http://dev.api.zo-ta.com");

        var providerService =
            providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
        var serviceCode = string.Empty;
        if (providerService != null)
            serviceCode = providerService.ServiceCode;
        else
            _logger.LogWarning(
                $"{payBillRequestLog.TransCode} ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");
        var request = new IoRequest
        {
            PartnerCode = providerInfo.Username, // "0866697567",
            ProductCode = serviceCode,
            PartnerTransId = payBillRequestLog.TransCode,
            BillingCode = payBillRequestLog.ReceiverInfo,
            PaidAmount = (long)payBillRequestLog.TransAmount
        };

        var sign = Sign(string.Join("", request.PartnerCode, request.PartnerTransId, request.ProductCode,
                request.BillingCode, request.PaidAmount),
            "./" + providerInfo.PrivateKeyFile);

        request.Sign = sign;

        _logger.LogInformation($"{payBillRequestLog.TransCode} Paybill object send: " + request.ToJson());

        IoResponse result = null;
        try
        {
            result = await client.PostAsync<IoResponse>("/IPayService/rest/partner/payBill", request);
        }
        catch (Exception e)
        {
            _logger.LogError($"{payBillRequestLog.TransCode} Paybill exception: " + e.Message);
            result = new IoResponse
            {
                ResCode = "501102", //Tự quy định mã này cho trường hợp timeout.
                ResMessage = e.Message
            };
        }

        if (result != null)
        {
            payBillRequestLog.ModifiedDate = DateTime.Now;
            payBillRequestLog.ResponseInfo = request.ToJson();
            _logger.Log(LogLevel.Information,
                $"{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode} Paybill return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");


            responseMessage.ProviderResponseCode = result?.ResCode;
            responseMessage.ProviderResponseMessage = result?.ResMessage;

            if (result.ResCode == "00")
            {
                payBillRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                responseMessage.Payload = result.Amount;
            }
            else if (new[] { "08", "501102" }.Contains(result.ResCode))
            {
                payBillRequestLog.Status = TransRequestStatus.Timeout;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }
            else
            {
                var reResult =
                    await _topupGatewayService.GetResponseMassageCacheAsync("IOMEDIA", result.ResCode,
                        payBillRequestLog.TransCode);

                payBillRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.ResMessage;
            }
        }
        else
        {
            _logger.LogInformation($"{payBillRequestLog.TransCode} Error send request");
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            payBillRequestLog.Status = TransRequestStatus.Fail;
        }

        await _topupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);


        return responseMessage;
    }

    private string Sign(string dataToSign, string privateFile)
    {
        var privateKeyText = File.ReadAllText("files/" + privateFile);
        var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
        var privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);
        using var rsa = RSA.Create();

        if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
            rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);

        else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY") rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
        var sig = rsa.SignData(
            Encoding.UTF8.GetBytes(dataToSign),
            HashAlgorithmName.SHA1,
            RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(sig);
        return signature;
    }

    private List<CardRequestResponseDto> GenDecryptListCode(string privateFile,
        List<CardRequestResponseDto> cardList, bool isTripDes = false)
    {
        try
        {
            var privateKeyText = File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);
            using var rsa = RSA.Create();
            if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY")
                rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

            foreach (var item in cardList)
            {
                var souceBytesCode = Convert.FromBase64String(item.CardCode);
                var bytesCode = rsa.Decrypt(souceBytesCode, RSAEncryptionPadding.Pkcs1);
                var stringCode = Encoding.UTF8.GetString(bytesCode, 0, bytesCode.Length);
                item.CardCode = isTripDes ? stringCode.EncryptTripDes() : stringCode;
            }

            return cardList;
        }
        catch (Exception ex)
        {
            _logger.LogError("GenDecryptListCode exception: " + ex.Message);
            return cardList;
        }
    }

    private DateTime TimeStampToDate(string timeStamp)
    {
        try
        {
            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddMilliseconds(Convert.ToDouble(timeStamp)).ToLocalTime();
            return dtDateTime;
        }
        catch (Exception ex)
        {
            _logger.LogError("TimeStampToDate exception: " + ex.Message);
            return DateTime.Now.AddYears(1);
        }
    }

    public Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
    {
        throw new NotImplementedException();
    }

    public Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode)
    {
        throw new NotImplementedException();
    }

    private async Task<List<CardRequestResponseDto>> GetRetrieveCardInfo(ProviderInfoDto providerInfo,
        string transCodeToCheck)
    {
        try
        {
            var request = new IoRequest
            {
                PartnerCode = providerInfo.Username,
                PartnerTransId = transCodeToCheck
            };
            var sign = Sign(string.Join("", request.PartnerCode, request.PartnerTransId),
                "./" + providerInfo.PrivateKeyFile);

            request.Sign = sign;
            _logger.LogInformation($"{providerInfo.ProviderCode}-{transCodeToCheck} IoMediaConnector Retrieve send: " +
                                   request.ToJson());
            var client = new JsonHttpClient(providerInfo.ApiUrl)
            {
                HttpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                    BaseAddress = new Uri(providerInfo.ApiUrl)
                }
            };

            var result = await client.PostAsync<IoResponse>("/IPayService/rest/partner/retrieveCardInfo", request);

            var cardList = result.CardList.Select(card => new CardRequestResponseDto
            {
                CardCode = card.Pincode,
                Serial = card.Serial,
                ExpireDate = TimeStampToDate(card.Expiredate).ToString("dd/MM/yyyy"),
                ExpiredDate = TimeStampToDate(card.Expiredate)
            }).ToList();
            cardList = GenDecryptListCode(providerInfo.PrivateKeyFile, cardList, isTripDes: true);
            return cardList;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{transCodeToCheck} IoMediaConnector Retrieve exception: " + ex.Message);
            return new List<CardRequestResponseDto>();
        }
    }
}