using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.Common;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.Shared.Utils;
using HLS.Paygate.TopupGw.Contacts.ApiRequests;
using HLS.Paygate.TopupGw.Contacts.Dtos;
using HLS.Paygate.TopupGw.Contacts.Enums;
using HLS.Paygate.TopupGw.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Components.Connectors.ZoTa
{
    public class ZotaConnector : IGatewayConnector
    {
        private readonly ILogger<ZotaConnector> _logger; // = LogManager.GetLogger("ZotaConnector");
        private readonly ITopupGatewayService _topupGatewayService;
        private readonly IBusControl _bus;
        private readonly ICacheManager _cacheManager;

        public ZotaConnector(ITopupGatewayService topupGatewayService, ILogger<ZotaConnector> logger,
            IBusControl bus, ICacheManager cacheManager)
        {
            _topupGatewayService = topupGatewayService;
            _logger = logger;
            _cacheManager = cacheManager;
            _bus = bus;
        }

        public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
            ProviderInfoDto providerInfo)
        {
            using (_logger.BeginScope(topupRequestLog.TransCode))
            {
                _logger.LogInformation($"ZotaConnector request:{topupRequestLog.TransRef}-{topupRequestLog.TransCode}");
                var responseMessage = new MessageResponseBase();
                try
                {
                    var key =
                        $"PayGate_TopupRequest:Items:{string.Join("_", topupRequestLog.ProviderCode, topupRequestLog.TransCode)}";
                    if (!_topupGatewayService.ValidConnector(ProviderConst.ZOTA, providerInfo.ProviderCode))
                    {
                        _logger.LogError(
                            $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-ZotaConnector ProviderConnector not valid");
                        return new MessageResponseBase
                        {
                            ResponseCode = ResponseCodeConst.Error,
                            ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                        };
                    }

                    var client = new JsonHttpClient(providerInfo.ApiUrl)
                    {
                        HttpClient = new HttpClient()
                        {
                            Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                            BaseAddress = new Uri(providerInfo.ApiUrl)
                        }
                    };
                    var request = new ZoTaRequest
                    {
                        Username = providerInfo.Username, // "0866697567",
                        ApiCode = providerInfo.ExtraInfo, //"042362",
                        ApiUsername = providerInfo.ApiUser, //"r77i42ha4mbkr80l4cez",
                        RequestId = topupRequestLog.TransCode,
                        TelcoType = topupRequestLog.Vendor,
                        TelcoServiceType = topupRequestLog.ServiceCode == "TOPUP" ? "PREPAID" :
                            topupRequestLog.ServiceCode == "PAY_BILL" ? "POSTPAID" : "",
                        Amount = topupRequestLog.TransAmount,
                        Msisdn = topupRequestLog.ReceiverInfo
                    };
                    responseMessage.TransCodeProvider = topupRequestLog.TransCode;

                    //Correct for call to partner
                    if (request.TelcoType == "VTE")
                        request.TelcoType = "VTM";
                    if (request.TelcoType == "VNA")
                        request.TelcoType = "VNP";

                    try
                    {
                        var sign = Sign(string.Join("|", request.Username, request.ApiCode, request.ApiUsername,
                            request.TelcoType,
                            request.Msisdn, request.Amount, request.RequestId), "./" + providerInfo.PrivateKeyFile);

                        request.DataSign = sign;
                    }
                    catch (Exception e)
                    {
                        _logger.LogInformation("Error sign data: " + e.Message);
                        topupRequestLog.Status = TransRequestStatus.Fail;
                        responseMessage.Exception = e.Message;
                        await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
                    }

                    ZoTaResponse result = null;
                    using (_logger.BeginScope("Send request to provider"))
                    {
                        _logger.LogInformation("ZotaConnector send: " + request.ToJson());
                        try
                        {
                            result = await client.PostAsync<ZoTaResponse>("/api/v1/partner/service/ptu/topup", request);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("ZotaConnector exception: " + ex);
                            result = new ZoTaResponse
                            {
                                Status = new Status
                                {
                                    Code = 501102, //Tự quy định mã này cho trường hợp timeout.
                                    Value = "Giao dịch đang chờ kết quả xử lý!"
                                }
                            };
                        }
                    }

                    if (result != null)
                    {
                        //Console.WriteLine("Result: " + result.ToJson());
                        topupRequestLog.ModifiedDate = DateTime.Now;
                        topupRequestLog.ResponseInfo = result.ToJson();

                        _logger.Log(LogLevel.Information,
                            $"ZotaConnector return: {topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{result.ToJson()}");
                        if (result.Status.Code == 0)
                        {
                            if (request.TelcoType != "VTM")
                            {
                                topupRequestLog.Status = TransRequestStatus.Success;
                                responseMessage.ResponseCode = ResponseCodeConst.Success;
                                responseMessage.ResponseMessage = "Giao dịch thành công";
                            }
                            else
                            {
                                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                                responseMessage.ResponseMessage = "Giao dịch được tiếp nhận";
                            }
                        }
                        else if (new[] { 4501, 501102 }.Contains(result.Status.Code))
                        {
                            topupRequestLog.Status = TransRequestStatus.Timeout;
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage =
                                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                        }
                        else
                        {
                            var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("ZOTA",
                                result.Status.Code.ToString(), topupRequestLog.TransCode);
                            topupRequestLog.Status = TransRequestStatus.Fail;
                            responseMessage.ResponseCode =
                                reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                            responseMessage.ResponseMessage =
                                reResult != null ? reResult.ReponseName : result.Status.Value;
                        }

                        if (request.TelcoType == "VTM" && new[] { 0, 4501, 501102 }.Contains(result.Status.Code))
                        {
                            var begin = DateTime.Now;
                            Thread.Sleep(2000);
                            var transCodeNew = topupRequestLog.TransCode + "_" + DateTime.Now.ToString("HHmmss");
                            responseMessage = await TransactionCheckAsync(topupRequestLog.ProviderCode,
                                topupRequestLog.TransCode, transCodeNew, serviceCode: string.Empty, providerInfo);
                            var noQuery = 1;
                            while (responseMessage.ResponseCode == ResponseCodeConst.ResponseCode_WaitForResult)
                            {
                                Thread.Sleep(1000);
                                var responseCache = await _cacheManager.GetEntity<TopupRequestLogDto>(key);
                                if (responseCache != null)
                                {
                                    if (responseCache.Status == TransRequestStatus.Success ||
                                        responseCache.Status == TransRequestStatus.Fail)
                                    {
                                        topupRequestLog.Status = responseCache.Status;
                                        responseMessage.ResponseCode =
                                            responseCache.Status == TransRequestStatus.Success
                                                ? ResponseCodeConst.ResponseCode_Success
                                                : ResponseCodeConst.Error;
                                        responseMessage.ResponseMessage = "Ket qua tu cache callBack_NCC";
                                        _logger.LogInformation(topupRequestLog.TransCode +
                                                               $"ZotaConnector Lay_ket_qua_tu cache: {responseCache.ToJson()}");
                                    }
                                }

                                if (responseMessage.ResponseCode == ResponseCodeConst.ResponseCode_WaitForResult)
                                {
                                    transCodeNew = topupRequestLog.TransCode + "_" + DateTime.Now.ToString("HHmmss");
                                    responseMessage = await TransactionCheckAsync(topupRequestLog.ProviderCode,
                                        topupRequestLog.TransCode, transCodeNew, serviceCode: string.Empty,
                                        providerInfo);
                                }

                                var end = DateTime.Now;
                                noQuery++;
                                if (end.Subtract(begin).TotalSeconds > providerInfo.TimeoutProvider)
                                {
                                    if (responseMessage.ResponseCode == ResponseCodeConst.ResponseCode_WaitForResult)
                                    {
                                        var timmeQuery = await _topupGatewayService.GetTopupRequestLogAsync(
                                            topupRequestLog.TransRef,
                                            topupRequestLog.ProviderCode);
                                        if (timmeQuery != null && (timmeQuery.Status == TransRequestStatus.Success ||
                                                                   timmeQuery.Status == TransRequestStatus.Fail))
                                        {
                                            _logger.Log(LogLevel.Information,
                                                "Get TopupLog reponse : " + timmeQuery.ToJson());
                                            responseMessage = new MessageResponseBase()
                                            {
                                                ResponseCode = timmeQuery.Status == TransRequestStatus.Success
                                                    ? ResponseCodeConst.ResponseCode_Success
                                                    : ResponseCodeConst.Error,
                                                ResponseMessage = "Ket qua tu callBack",
                                            };

                                            topupRequestLog.Status = timmeQuery.Status;
                                        }
                                    }

                                    break;
                                }
                            }

                            topupRequestLog.Status = responseMessage.ResponseCode == ResponseCodeConst.Success
                                ? TransRequestStatus.Success
                                : responseMessage.ResponseCode == ResponseCodeConst.ResponseCode_WaitForResult
                                    ? TransRequestStatus.Timeout
                                    : topupRequestLog.Status;
                        }

                        responseMessage.ProviderResponseCode = result?.Status.Code.ToFormat();
                        responseMessage.ProviderResponseMessage = result?.Status.Value;
                    }
                    else
                    {
                        _logger.LogInformation("Error send request");
                        responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
                        topupRequestLog.Status = TransRequestStatus.Fail;
                    }

                    await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
                    await _cacheManager.ClearCache(key);
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
                _logger.LogInformation($"{transCodeToCheck} ZotaConnector check request: " + transCode);
                var responseMessage = new MessageResponseBase();

                if (providerInfo == null)
                    providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

                if (providerInfo == null ||
                    !_topupGatewayService.ValidConnector(ProviderConst.ZOTA, providerInfo.ProviderCode))
                {
                    _logger.LogError($"{transCode}-{providerCode}-ZotaConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                    };
                }

                var client = new JsonHttpClient(providerInfo.ApiUrl)
                {
                    HttpClient = new HttpClient()
                    {
                        Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                        BaseAddress = new Uri(providerInfo.ApiUrl)
                    }
                };

                var request = new ZoTaRequest
                {
                    Username = providerInfo.Username, // "0866697567",
                    ApiCode = providerInfo.ExtraInfo, //"042362",
                    ApiUsername = providerInfo.ApiUser, //"r77i42ha4mbkr80l4cez",
                    RequestId = transCode,
                    ReferenceId = transCodeToCheck
                };
                var sign = Sign(string.Join("|", request.Username, request.ApiCode, request.ApiUsername,
                    request.RequestId), "./" + providerInfo.PrivateKeyFile);

                request.DataSign = sign;
                _logger.LogInformation($"{transCodeToCheck} ZotaConnector check send: " + request.ToJson());
                ZoTaResponse result = null;
                try
                {
                    result = await client.PostAsync<ZoTaResponse>("/api/v1/partner/service/check-transaction", request);
                    //var result2 = await client.PostAsync<string>("/api/v1/partner/service/check-transaction", request);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{transCodeToCheck} ZotaConnector check exception: " + ex.Message);
                    result = new ZoTaResponse
                    {
                        Status = new Status
                        {
                            Code = 501102, //Tự quy định mã này cho trường hợp timeout.
                            Value = "Giao dịch đang chờ kết quả xử lý!"
                        },
                        Transaction = new Transaction()
                        {
                            TransactionStatus = "501102"
                        },
                    };
                }

                if (result != null)
                {
                    _logger.Log(LogLevel.Information,
                        $"{transCodeToCheck}{providerCode}- ZotaConnector check return: {transCode}-{transCodeToCheck}-{result.ToJson()}");
                    if (result.Transaction != null)
                    {
                        if (result.Transaction.TransactionStatus == "10")
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.Success;
                            responseMessage.ResponseMessage = "Giao dịch thành công";
                            responseMessage.Payload = result.Transaction;
                        }
                        else if (new[] { "3", "5", "7" }.Contains(result.Transaction.TransactionStatus))
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.Error;
                            responseMessage.ResponseMessage = "Giao dịch lỗi";
                            responseMessage.Payload = result.Transaction;
                        }
                        else
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage =
                                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                            responseMessage.Payload = result.Transaction;
                        }

                        responseMessage.ProviderResponseCode = result?.Transaction.TransactionStatus;
                        responseMessage.ProviderResponseMessage = result?.Transaction.ErrorMessage;
                    }
                    else
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = "Giao dịch không ghi nhận bên NCC";
                    }
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
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new MessageResponseBase
                {
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                    ResponseCode = ResponseCodeConst.ResponseCode_TimeOut,
                    Exception = e.Message
                };
            }
        }

        public async Task<NewMessageReponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
        {
            _logger.Log(LogLevel.Information,
                $"{payBillRequestLog.TransCode} ZotaConnector query request: " + payBillRequestLog.ToJson());
            var responseMessage = new NewMessageReponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Truy vấn thông tin không thành công")
            };
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.ZOTA, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-ZotaConnector ProviderConnector not valid");
                responseMessage.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                    "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ");
                return responseMessage;
            }

            var client = new JsonHttpClient(providerInfo.ApiUrl)
            {
                HttpClient = new HttpClient()
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

            var request = new ZoTaRequest
            {
                Username = providerInfo.Username, // "0866697567",
                ApiCode = providerInfo.ExtraInfo, //"042362",
                ApiUsername = providerInfo.ApiUser, //"r77i42ha4mbkr80l4cez",
                RequestId = payBillRequestLog.TransCode,
                ServiceCode = serviceCode, // "050109", // transRequest.ServiceCode,
                InvoiceReference = payBillRequestLog.IsInvoice ? payBillRequestLog.ReceiverInfo : string.Empty,
                CustomerReference = !payBillRequestLog.IsInvoice ? payBillRequestLog.ReceiverInfo : string.Empty
            };

            var sign = Sign(string.Join("|", request.Username, request.ApiCode, request.ApiUsername,
                    request.ServiceCode,
                    request.InvoiceReference, request.CustomerReference, request.RequestId),
                "./" + providerInfo.PrivateKeyFile);

            request.DataSign = sign;

            _logger.LogInformation($"{payBillRequestLog.TransCode} ZotaConnector query send: " + request.ToJson());

            ZoTaResponse result;
            try
            {
                result = await client.PostAsync<ZoTaResponse>("/api/v1/partner/service/invoice/check", request);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{payBillRequestLog.TransCode} ZotaConnector query exception: " + ex.Message);
                _logger.LogError("Query time out");
                result = new ZoTaResponse
                {
                    Status = new Status
                    {
                        Code = 501102, //Tự quy định mã này cho trường hợp timeout.
                        Value = ex.Message
                    }
                };
            }

            if (result != null)
            {
                _logger.Log(LogLevel.Information,
                    $"{payBillRequestLog.TransCode} ZotaConnector query return: {payBillRequestLog.TransCode}-{result.ToJson()}");
                if (result.Status.Code == 0)
                {
                    responseMessage.ResponseStatus =
                        new ResponseStatusApi(ResponseCodeConst.Success, "Giao dịch thành công");
                    responseMessage.Results = SaleCommon.GetBillQueryInfo(result.Invoice);
                }
                else if (new[] { 4501, 501102 }.Contains(result.Status.Code))
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("ZOTA",
                        result.Status.Code.ToString(), payBillRequestLog.TransCode);
                    responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseStatus.Message = reResult != null
                        ? reResult.ReponseName
                        : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("ZOTA",
                        result.Status.Code.ToString(), payBillRequestLog.TransCode);
                    responseMessage.ResponseStatus.ErrorCode =
                        reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseStatus.Message =
                        reResult != null ? reResult.ReponseName : result.Status.Value;
                }
                //responseMessage.ProviderResponseCode = result?.Status.Code.ToString();
                //responseMessage.ProviderResponseMessage = result?.Status.Value;
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
                $"{cardRequestLog.TransCode} ZotaConnector card request: " + cardRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();

            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.ZOTA, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{providerInfo.ProviderCode}-ZotaConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var client = new JsonHttpClient(providerInfo.ApiUrl)
            {
                HttpClient = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                    BaseAddress = new Uri(providerInfo.ApiUrl)
                }
            }; //("http://dev.api.zo-ta.com");

            var request = new ZoTaRequest
            {
                Username = providerInfo.Username, // "0866697567",
                ApiCode = providerInfo.ExtraInfo, //"042362",
                ApiUsername = providerInfo.ApiUser, //"r77i42ha4mbkr80l4cez",
                RequestId = cardRequestLog.TransCode,
                CardType = cardRequestLog.Vendor,
                FaceValue = cardRequestLog.TransAmount.ToString("0"),
                Quantity = cardRequestLog.Quantity,
                Msisdn = cardRequestLog.ReceiverInfo
            };
            responseMessage.TransCodeProvider = cardRequestLog.TransCode;

            if (cardRequestLog.ProductCode.Contains("_PINDATA_"))
            {
                if (cardRequestLog.ProductCode.StartsWith("VNA"))
                {
                    request.CardType = "DT_VNP";
                }

                if (cardRequestLog.ProductCode.StartsWith("VMS"))
                    request.CardType = "DT_VMS";
                if (cardRequestLog.ProductCode.StartsWith("VTE"))
                    request.CardType = "DT_VTM";
            }

            //Correct for call to partner
            if (request.CardType == "VTE")
                request.CardType = "VTM";
            if (request.CardType == "VNA")
                request.CardType = "VNP";

            var sign = Sign(string.Join("|", request.Username, request.ApiCode, request.ApiUsername, request.CardType,
                request.FaceValue, request.Quantity, request.RequestId), "./" + providerInfo.PrivateKeyFile);

            request.DataSign = sign;

            _logger.LogInformation("Card object send: " + request.ToJson());

            ZoTaResponse result;
            try
            {
                result = await client.PostAsync<ZoTaResponse>("/api/v1/partner/service/ptu/epin", request);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{cardRequestLog.TransCode} Get Cards exception: " + ex.Message);
                result = new ZoTaResponse
                {
                    Status = new Status
                    {
                        Code = 501102, //Tự quy định mã này cho trường hợp timeout.
                        Value = "Giao dịch đang chờ kết quả xử lý!"
                    }
                };
            }

            if (result != null)
            {
                cardRequestLog.ModifiedDate = DateTime.Now;
                cardRequestLog.ResponseInfo = result.ToJson();
                _logger.Log(LogLevel.Information,
                    $"ZotaConnector return: {cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{result.ToJson()}");
                if (result.Status.Code == 0)
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    cardRequestLog.Status = TransRequestStatus.Success;
                    try
                    {
                        var cards = Cryptography.TripleDesDecrypt(result.EncryptedCards,
                            providerInfo.ApiPassword); //"ji15weck0ygrleb8sdrchul1zplf71zf");

                        var cardTextList = cards.Trim(';').Split(';');
                        var cardList = new List<CardRequestResponseDto>();
                        foreach (var cardText in cardTextList)
                        {
                            if (!string.IsNullOrEmpty(cardText))
                            {
                                cardList.Add(new CardRequestResponseDto
                                {
                                    CardType = cardText.Split('|')[0],
                                    CardValue = cardText.Split('|')[1],
                                    CardCode = cardText.Split('|')[2],
                                    Serial = cardText.Split('|')[3],
                                    ExpireDate = cardText.Split('|')[4],
                                    ExpiredDate = getExpireDate(cardText.Split('|')[4]),
                                });
                            }
                        }

                        responseMessage.Payload = cardList;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"{cardRequestLog.TransCode} Error parsing cards: " + e.Message);
                    }
                }
                else if (new[] { 4501, 501102 }.Contains(result.Status.Code))
                {
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("ZOTA",
                        result.Status.Code.ToString(), cardRequestLog.TransCode);
                    responseMessage.ResponseMessage =
                        reResult != null
                            ? reResult.ReponseName
                            : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("ZOTA",
                        result.Status.Code.ToString(), cardRequestLog.TransCode);
                    cardRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : result.Status.Value;
                }

                responseMessage.ProviderResponseCode = result?.Status.Code.ToString();
                responseMessage.ProviderResponseMessage = result?.Status.Value;
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
            _logger.Log(LogLevel.Information, $"{transCode} ZotaConnector balance request: " + transCode);
            var responseMessage = new MessageResponseBase();

            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.ZOTA, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{providerCode}-{transCode}-{providerInfo.ProviderCode}-ZotaConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var client = new JsonHttpClient(providerInfo.ApiUrl)
            {
                HttpClient = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                    BaseAddress = new Uri(providerInfo.ApiUrl)
                }
            }; //("http://dev.api.zo-ta.com");

            var request = new ZoTaRequest
            {
                Username = providerInfo.Username, // "0866697567",
                ApiCode = providerInfo.ExtraInfo, //"042362",
                ApiUsername = providerInfo.ApiUser, //"r77i42ha4mbkr80l4cez",
                RequestId = transCode
            };
            var sign = Sign(string.Join("|", request.Username, request.ApiCode, request.ApiUsername,
                request.RequestId), "./" + providerInfo.PrivateKeyFile);

            request.DataSign = sign;
            _logger.LogInformation($"{transCode} Balance object send: " + request.ToJson());

            ZoTaResponse result = null;
            try
            {
                result = await client.PostAsync<ZoTaResponse>("/api/v1/partner/service/check-balance", request);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{transCode} Balance exception: " + ex.Message);
                result = new ZoTaResponse
                {
                    Status = new Status
                    {
                        Code = 501102, //Tự quy định mã này cho trường hợp timeout.
                        Value = ex.Message
                    }
                };
            }

            if (result != null)
            {
                _logger.Log(LogLevel.Information, $"{transCode} Balance return: {transCode}-{result.ToJson()}");
                if (result.Status.Code == 0)
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.Payload = result.Balance;
                }
                else if (new[] { 4501, 501102 }.Contains(result.Status.Code))
                {
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync("ZOTA", result.Status.Code.ToString(),
                            transCode);
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        reResult != null
                            ? reResult.ReponseName
                            : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync("ZOTA", result.Status.Code.ToString(),
                            transCode);
                    responseMessage.ResponseCode = reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : result.Status.Value;
                }
            }
            else
            {
                _logger.LogInformation($"{transCode} Error send request");
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
            _logger.Log(LogLevel.Information,
                $"{payBillRequestLog.TransCode} ZotaConnector Paybill request: " + payBillRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();

            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.ZOTA, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-ZotaConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var client = new JsonHttpClient(providerInfo.ApiUrl)
            {
                HttpClient = new HttpClient()
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
            var request = new ZoTaRequest
            {
                Username = providerInfo.Username, // "0866697567",
                ApiCode = providerInfo.ExtraInfo, //"042362",
                ApiUsername = providerInfo.ApiUser, //"r77i42ha4mbkr80l4cez",
                RequestId = payBillRequestLog.TransCode,
                ServiceCode = serviceCode, // transRequest.ServiceCode,
                InvoiceReference = payBillRequestLog.IsInvoice ? payBillRequestLog.ReceiverInfo : string.Empty,
                CustomerReference = !payBillRequestLog.IsInvoice ? payBillRequestLog.ReceiverInfo : string.Empty,
                Amount = payBillRequestLog.TransAmount,
                PayAll = payBillRequestLog.PayAll
            };

            responseMessage.TransCodeProvider = payBillRequestLog.TransCode;
            var sign = Sign(string.Join("|", request.Username, request.ApiCode, request.ApiUsername,
                    request.ServiceCode,
                    request.InvoiceReference, request.CustomerReference, request.Amount,
                    request.PayAll.ToString().ToLower(), request.RequestId),
                "./" + providerInfo.PrivateKeyFile);

            request.DataSign = sign;

            _logger.LogInformation($"{payBillRequestLog.TransCode} Paybill object send: " + request.ToJson());

            ZoTaResponse result = null;
            try
            {
                result = await client.PostAsync<ZoTaResponse>("/api/v1/partner/service/invoice/pay", request);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{payBillRequestLog.TransCode} ZotaConnector exception: " + ex);
                result = new ZoTaResponse
                {
                    Status = new Status
                    {
                        Code = 501102, //Tự quy định mã này cho trường hợp timeout.
                        Value = ex.Message
                    }
                };
            }

            if (result != null)
            {
                payBillRequestLog.ModifiedDate = DateTime.Now;
                payBillRequestLog.ResponseInfo = request.ToJson();
                _logger.Log(LogLevel.Information,
                    $"{payBillRequestLog.TransCode} ZotaConnector return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
                if (result.Status.Code == 0)
                {
                    payBillRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.Payload = result.Transaction;
                }
                else if (new[] { 4501, 501102 }.Contains(result.Status.Code))
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("ZOTA",
                        result.Status.Code.ToString(), payBillRequestLog.TransCode);
                    payBillRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        reResult != null
                            ? reResult.ReponseName
                            : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("ZOTA",
                        result.Status.Code.ToString(), payBillRequestLog.TransCode);
                    payBillRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : result.Status.Value;
                }

                responseMessage.ProviderResponseCode = result?.Status.Code.ToString();
                responseMessage.ProviderResponseMessage = result?.Status.Value;
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

        private DateTime getExpireDate(string expireDate)
        {
            try
            {
                if (string.IsNullOrEmpty(expireDate))
                    return new DateTime(DateTime.Now.AddYears(2).Year, 12, 31);

                var s = expireDate.Split('-', '/');
                return new DateTime(Convert.ToInt32(s[0]), Convert.ToInt32(s[1]), Convert.ToInt32(s[2]));
            }
            catch (Exception e)
            {
                _logger.LogError($"{expireDate} getExpireDate Error convert_date : " + e.Message);
                return new DateTime(DateTime.Now.AddYears(2).Year, 12, 31);
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
    }
}