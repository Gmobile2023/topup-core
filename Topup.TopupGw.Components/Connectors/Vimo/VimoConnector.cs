using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.CacheManager;
using Topup.Shared.Dtos;
using Topup.Shared.Utils;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using Topup.TopupGw.Domains.BusinessServices;
using Topup.TopupGw.Domains.Entities;
using Microsoft.Extensions.Logging;
using RestSharp;
using ServiceStack;
using ServiceStack.Script;

namespace Topup.TopupGw.Components.Connectors.Vimo;

public class VimoConnector : IGatewayConnector
{
    private readonly ICacheManager _cacheManager;
    private readonly ILogger<VimoConnector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;


    public VimoConnector(ITopupGatewayService topupGatewayService,
        ILogger<VimoConnector> logger, ICacheManager cacheManager)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
        _cacheManager = cacheManager;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        _logger.LogInformation("VimoConnector request: " + topupRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        if (!_topupGatewayService.ValidConnector(ProviderConst.VIMO, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-VimoConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var str = topupRequestLog.ProductCode.Split('_');
        var keyCode = $"{str[0]}_{str[1]}";

        var providerService =
            providerInfo.ProviderServices.Find(p => p.ProductCode == keyCode);
        var serviceCode = string.Empty;
        var publisher = string.Empty;
        if (providerService != null)
        {
            serviceCode = providerService.ServiceCode.Split('|')[0];
            publisher = providerService.ServiceCode.Split('|')[1];
        }
        else
        {
            _logger.LogInformation(
                $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef} ProviderService with ProductCode [{topupRequestLog.ProductCode}] is null");
        }

        var json = new TopupDto
        {
            mc_request_id = topupRequestLog.TransCode,
            service_code = serviceCode,
            publisher = publisher,
            receiver = topupRequestLog.ReceiverInfo,
            amount = topupRequestLog.TransAmount
        }.ToJson();

        _logger.LogInformation($"{topupRequestLog.TransCode} VimoConnector Param_Json: " + json);

        var encrypt = new VimoAes256().Encrypt(json, providerInfo.Password);
        var strCheckum = providerInfo.Username + encrypt + providerInfo.PublicKey;

        var request = new VimoRequest
        {
            fnc = topupRequestLog.ProductCode.StartsWith("TOPUPDATA") ? "topupdata" : "topup",
            Merchantcode = providerInfo.Username,
            data = encrypt,
            Checksum = strCheckum.EncryptMd5()
        };

        var result = await CallApi(providerInfo, request, topupRequestLog.TransCode);
        _logger.LogInformation($"{topupRequestLog.TransCode}-{topupRequestLog.TransRef} VimoConnector Topup Reponse: " +
                               result.ToJson());

        try
        {
            if (result != null && result.error_code == ResponseCodeConst.Error)
            {
                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.ResponseInfo = result.ToJson();
                topupRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
            }
            else
            {
                if (result != null && providerInfo.ExtraInfo.Contains(result.error_code))
                {
                    topupRequestLog.ModifiedDate = DateTime.Now;
                    topupRequestLog.ResponseInfo = result.ToJson();
                    topupRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch lỗi phía NCC";
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VIMO,
                        result.error_code, topupRequestLog.TransCode);
                    responseMessage.ResponseCode = reResult != null
                        ? reResult.ResponseCode
                        : ResponseCodeConst.ResponseCode_ErrorProvider;
                    responseMessage.ResponseMessage = reResult != null
                        ? reResult.ResponseName
                        : "Giao dịch lỗi phía NCC";
                }
                else
                {
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                    topupRequestLog.Status = TransRequestStatus.Timeout;
                    topupRequestLog.ModifiedDate = DateTime.Now;
                }
            }

            responseMessage.ProviderResponseCode = result?.error_code;
            responseMessage.ProviderResponseMessage = result?.error_message;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(
                $"VimoConnector Error: {topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()} Exception: {ex}");
            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
            topupRequestLog.Status = TransRequestStatus.Timeout;
        }
        finally
        {
            await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
        }

        return responseMessage;
    }

    public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
    {
        try
        {
            _logger.LogInformation($"{transCodeToCheck} VimoConnector CheckTrans request: " + transCode);
            var responseMessage = new MessageResponseBase();

            if (providerInfo == null)
                providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);


            if (providerInfo == null ||
                !_topupGatewayService.ValidConnector(ProviderConst.VIMO, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{transCodeToCheck} - {providerCode}-VimoConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            var historyLog = await _topupGatewayService.GetTopupGateTransCode(transCodeToCheck, serviceCode);
            var providerService = new ProviderServiceDto();
            var productCode = "";
            if (historyLog.ProductCode.Contains("PIN") || historyLog.ProductCode.Contains("TOPUP"))
                productCode = historyLog.ProductCode.Split("_")[0] + "_" + historyLog.ProductCode.Split("_")[1];
            else productCode = historyLog.ProductCode;

            providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == productCode);
            var serviceCodeProvider = string.Empty;
            var publisher = string.Empty;
            if (providerService != null)
            {
                serviceCodeProvider = providerService.ServiceCode.Split('|')[0];
                publisher = providerService.ServiceCode.Split('|')[1];
            }

            string function = serviceCode == ServiceCodes.PAY_BILL
                ? "checkbilltransaction"
                : serviceCode == ServiceCodes.TOPUP || serviceCode == ServiceCodes.TOPUP_DATA
                    ? "checktopuptransaction"
                    : "getpincodetransaction";

            var json = new
            {
                mc_request_id = transCodeToCheck,
                service_code = serviceCodeProvider,
            }.ToJson();

            _logger.LogInformation($"{transCodeToCheck} VimoConnector CheckTran Param_Json: " + json);

            var encrypt = new VimoAes256().Encrypt(json, providerInfo.Password);
            var strCheckum = providerInfo.Username + encrypt + providerInfo.PublicKey;

            var request = new VimoRequest
            {
                fnc = function,
                Merchantcode = providerInfo.Username,
                data = encrypt,
                Checksum = strCheckum.EncryptMd5()
            };

            var result = await CallApi(providerInfo, request, transCodeToCheck);
            _logger.LogInformation(
                $"{transCodeToCheck}-{transCode} VimoConnector CheckTran Reponse: " + result.ToJson());

            if (result != null && result.error_code == ResponseCodeConst.Error)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";

                try
                {
                    if (function == "getpincodetransaction")
                    {
                        var info = result.data.ConvertTo<PinCodeInfoDto>();
                        var cards = info.cards;
                        var cardList = new List<CardRequestResponseDto>();
                        foreach (var card in cards)
                            cardList.Add(new CardRequestResponseDto
                            {
                                CardValue = card.cardValue,
                                CardCode = card.cardCode.EncryptTripDes(),
                                Serial = card.cardSerial,
                                ExpireDate = card.expiryDate,
                                ExpiredDate = getExpireDate(card.expiryDate)
                            });

                        responseMessage.Payload = cardList;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"transCodeToCheck= {transCodeToCheck} cards_Exception: {e.Message}");
                }
            }
            // else if (result != null && (providerInfo.ExtraInfo ?? "").Contains(result.error_code))
            // {
            //     responseMessage.ResponseCode = ResponseCodeConst.Error;
            //     responseMessage.ResponseMessage = "Giao dịch không thành công";
            // }
            else
            {
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }

            responseMessage.ProviderResponseCode = result?.error_code;
            responseMessage.ProviderResponseMessage = result?.error_message;
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

    public async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.LogInformation("QueryAsync request: " + payBillRequestLog.ToJson());
        var responseMessage = new NewMessageResponseBase<InvoiceResultDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Truy vấn thông tin không thành công")
        };
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

        if (providerInfo == null)
        {
            _logger.LogInformation("providerInfo is null");
            return responseMessage;
        }

        if (!_topupGatewayService.ValidConnector(ProviderConst.VIMO, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-VimoConnector ProviderConnector not valid");
            responseMessage.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ");
            return responseMessage;
        }

        var providerService =
            providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
        string serviceCode;
        string publisher;
        if (providerService != null)
        {
            serviceCode = providerService.ServiceCode.Split('|')[0];
            publisher = providerService.ServiceCode.Split('|')[1];
        }
        else
        {
            _logger.LogWarning(
                $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef} ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");
            return responseMessage;
        }

        var json = new
        {
            mc_request_id = payBillRequestLog.TransCode,
            service_code = serviceCode,
            publisher,
            customer_code = payBillRequestLog.ReceiverInfo
        }.ToJson();

        _logger.LogInformation(
            $"{payBillRequestLog.ReceiverInfo}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef} VimoConnector Param_Json: " +
            json);

        var encrypt = new VimoAes256().Encrypt(json, providerInfo.Password);
        var strCheckum = providerInfo.Username + encrypt + providerInfo.PublicKey;

        var request = new VimoRequest
        {
            fnc = "querybill",
            Merchantcode = providerInfo.Username,
            data = encrypt,
            Checksum = strCheckum.EncryptMd5()
        };

        var reponse = await CallApi(providerInfo, request, payBillRequestLog.TransCode);
        _logger.LogInformation(
            $"{payBillRequestLog.ReceiverInfo}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef} VimoConnector return: " +
            reponse.ToJson());
        if (reponse.error_code == ResponseCodeConst.Error)
        {
            var responseData = reponse.data.ConvertTo<VbillInfo>();
            responseMessage.ResponseStatus =
                new ResponseStatusApi(ResponseCodeConst.Success, "Giao dịch thành công");
            //if (mq.billDetail != null)
            // responseMessage.ExtraInfo = mq.billDetail[0].billNumber + "|" + mq.billDetail[0].billType;
            //Lưu và cache - Khi thanh toán dùng lại
            //await _cacheManager.SetValueIfNotExistsAsync(payBillRequestLog.ReceiverInfo, mq.billDetail[0].billType, TimeSpan.FromMinutes(15));
            if (responseData.billDetail != null)
            {
                var key = $"PayGate_BillQuery:Items:{payBillRequestLog.ReceiverInfo}_{payBillRequestLog.ProviderCode}";
                await _cacheManager.AddEntity(key, responseData.billDetail, TimeSpan.FromMinutes(15));
            }

            var dto = new InvoiceResultDto
            {
                Amount = decimal.Parse(responseData.billDetail != null
                    ? responseData.billDetail.Sum(c => c.amount).ToString()
                    : "0"),
                Period = responseData.billDetail != null ? responseData.billDetail[0].period : "",
                CustomerReference = payBillRequestLog.ReceiverInfo,
                CustomerName = responseData.customerInfo.customerName,
                Address = responseData.customerInfo.customerAddress,
                BillType = responseData.billDetail != null ? responseData.billDetail[0].billType : "",
                BillId = responseData.billDetail != null ? responseData.billDetail[0].billNumber : "",
                PeriodDetails = responseData.billDetail != null
                    ? (from x in responseData.billDetail
                        select new PeriodDto
                        {
                            Amount = x.amount,
                            Period = x.period,
                            BillNumber = x.billNumber,
                            BillType = x.billType
                        }).ToList()
                    : new List<PeriodDto>()
            };


            responseMessage.Results = dto;
        }
        else
        {
            var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("VIMO", reponse?.error_code,
                payBillRequestLog.TransCode);
            responseMessage.ResponseStatus.ErrorCode =
                reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
            responseMessage.ResponseStatus.Message =
                reResult != null ? reResult.ResponseName : reponse?.error_message;
            //responseMessage.ProviderResponseCode = reponse?.error_code;
            //responseMessage.ProviderResponseMessage = reponse?.error_message;
        }

        return responseMessage;
    }

    public async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        _logger.LogInformation("VimoConnector request: " + cardRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

        if (providerInfo == null)
        {
            _logger.LogInformation("providerInfo is null");
            return responseMessage;
        }

        if (!_topupGatewayService.ValidConnector(ProviderConst.VIMO, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{providerInfo.ProviderCode}-VimoConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var str = cardRequestLog.ProductCode.Split('_');
        var keyCode = $"{str[0]}_{str[1]}";
        var providerService =
            providerInfo.ProviderServices.Find(p => p.ProductCode == keyCode);
        var serviceCode = string.Empty;
        var publisher = string.Empty;
        if (providerService != null)
        {
            serviceCode = providerService.ServiceCode.Split('|')[0];
            publisher = providerService.ServiceCode.Split('|')[1];
        }
        else
        {
            _logger.LogInformation(
                $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef} ProviderService with ProductCode [{cardRequestLog.ProductCode}] is null");
        }

        var json = new VpinDto
        {
            mc_request_id = cardRequestLog.TransCode,
            service_code = serviceCode,
            quantity = cardRequestLog.Quantity,
            amount = Convert.ToInt32(cardRequestLog.TransAmount),
            publisher = publisher
        }.ToJson();

        _logger.LogInformation($"{cardRequestLog.TransCode} VimoConnector Param_Json: " + json);

        var encrypt = new VimoAes256().Encrypt(json, providerInfo.Password);
        var strCheckum = providerInfo.Username + encrypt + providerInfo.PublicKey;

        var request = new VimoRequest
        {
            fnc = "pincode",
            Merchantcode = providerInfo.Username,
            data = encrypt,
            Checksum = strCheckum.EncryptMd5()
        };

        var result = await CallApi(providerInfo, request, cardRequestLog.TransCode);
        _logger.LogInformation(
            $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef} VimoConnector Topup Reponse: {result.error_code}|{result.error_message}"
        );

        try
        {
            if (result != null && result.error_code == ResponseCodeConst.Error)
            {
                cardRequestLog.ModifiedDate = DateTime.Now;
                cardRequestLog.ResponseInfo = "";
                cardRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
                var info = result.data.ConvertTo<PinCodeInfoDto>();
                try
                {
                    var cards = info.cards;
                    var cardList = new List<CardRequestResponseDto>();
                    foreach (var card in cards)
                        cardList.Add(new CardRequestResponseDto
                        {
                            CardType = cardRequestLog.Vendor,
                            CardValue = card.cardValue,
                            CardCode = card.cardCode,
                            Serial = card.cardSerial,
                            ExpireDate = card.expiryDate,
                            ExpiredDate = getExpireDate(card.expiryDate)
                        });

                    responseMessage.Payload = cardList;
                }
                catch (Exception e)
                {
                    _logger.LogError($"{cardRequestLog.TransCode} Error parsing cards: " + e.Message);
                }
            }
            else
            {
                if (result != null && (providerInfo.ExtraInfo ?? "").Contains(result.error_code))
                {
                    cardRequestLog.ModifiedDate = DateTime.Now;
                    cardRequestLog.ResponseInfo = result.ToJson();
                    cardRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch lỗi phía NCC";
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VIMO,
                        result.error_code, cardRequestLog.TransCode);
                    responseMessage.ResponseCode = reResult != null
                        ? reResult.ResponseCode
                        : ResponseCodeConst.ResponseCode_ErrorProvider;
                    responseMessage.ResponseMessage =
                        reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                }
                else
                {
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    cardRequestLog.ModifiedDate = DateTime.Now;
                }
            }

            responseMessage.ProviderResponseCode = result?.error_code;
            responseMessage.ProviderResponseMessage = result?.error_message;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(
                $"VimoConnector Error: {cardRequestLog.ProviderCode}-{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{result.ToJson()} Exception: {ex}");
            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
            responseMessage.Exception = ex.Message;
            cardRequestLog.Status = TransRequestStatus.Timeout;
        }

        await _topupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
        return responseMessage;
    }

    public async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        _logger.LogInformation("QueryBalanceAsync request: " + providerCode);
        var responseMessage = new MessageResponseBase();
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

        if (providerInfo == null)
        {
            _logger.LogInformation($"providerCode= {providerCode}|providerInfo is null");
            return responseMessage;
        }

        if (!_topupGatewayService.ValidConnector(ProviderConst.VIMO, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{providerCode}-{transCode}-{providerInfo.ProviderCode}-VimoConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var json = new
        {
            mc_request_id = transCode,
            merchant_code = providerInfo.Username
        }.ToJson();

        _logger.LogInformation($"{transCode} VimoConnector Param_Json: " + json);

        var encrypt = new VimoAes256().Encrypt(json, providerInfo.Password);
        var strCheckum = providerInfo.Username + encrypt + providerInfo.PublicKey;

        var request = new VimoRequest
        {
            fnc = "getbalance",
            Merchantcode = providerInfo.Username,
            data = encrypt,
            Checksum = strCheckum.EncryptMd5()
        };

        var reponse = await CallApi(providerInfo, request, transCode);
        if (reponse.error_code == ResponseCodeConst.Error)
        {
            var balance = reponse.data.ConvertTo<BalanceDto>();
            responseMessage.ExtraInfo = Math.Round(Convert.ToDouble(balance.balance), 0).ToString();
            responseMessage.Payload = responseMessage.ExtraInfo;
            responseMessage.ResponseMessage = "Thành công";
            responseMessage.ResponseCode = ResponseCodeConst.Success;
        }

        return responseMessage;
    }

    public Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
    {
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.LogInformation($"{payBillRequestLog.TransCode} VimoConnector Paybill request: " +
                               payBillRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.VIMO, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-ZotaConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var providerService =
            providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
        var serviceCode = string.Empty;
        var publisher = string.Empty;
        if (providerService != null)
        {
            serviceCode = providerService.ServiceCode.Split('|')[0];
            publisher = providerService.ServiceCode.Split('|')[1];
        }
        else
        {
            _logger.LogWarning(
                $"{payBillRequestLog.TransCode} ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");
        }

        var key = $"PayGate_BillQuery:Items:{payBillRequestLog.ReceiverInfo}_{payBillRequestLog.ProviderCode}";
        var objCache = await _cacheManager.GetEntity<List<billDetailDto>>(key);
        if (objCache == null)
        {
            var dtoQuery = new PayBillRequestLogDto
            {
                ProductCode = payBillRequestLog.ProductCode,
                ProviderCode = payBillRequestLog.ProviderCode,
                CategoryCode = payBillRequestLog.CategoryCode,
                ReceiverInfo = payBillRequestLog.ReceiverInfo,
                TransCode = payBillRequestLog.ReceiverInfo + "_" + DateTime.Now.ToString("yyMMddHHmmss")
            };
            var query = await QueryAsync(dtoQuery);
            if (query.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                objCache = (from x in query.Results.PeriodDetails
                    select new billDetailDto
                    {
                        amount = Convert.ToInt32(x.Amount),
                        period = x.Period,
                        billNumber = x.BillNumber,
                        billType = x.BillType
                    }).ToList();
        }

        if (objCache == null && objCache.Count == 0)
        {
            _logger.LogInformation($"{payBillRequestLog.TransCode} VimoConnector Paybill cannot get QueryInfo");
            responseMessage.ResponseMessage = "Giao dịch không thành công. Vui lòng thử lại sau";
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            payBillRequestLog.Status = TransRequestStatus.Fail;
            return responseMessage;
        }

        var arrays = new List<bill_payment>();
        var amount = payBillRequestLog.TransAmount;
        foreach (var x in objCache)
        {
            if (amount >= x.amount)
            {
                var obj = new bill_payment
                {
                    amount = x.amount,
                    period = x.period,
                    billNumber = x.billNumber,
                    billType = x.billType
                };
                arrays.Add(obj);
                amount = amount - x.amount;
            }
            else
            {
                var obj = new bill_payment
                {
                    amount = Convert.ToInt32(amount),
                    period = x.period,
                    billNumber = x.billNumber,
                    billType = x.billType
                };
                arrays.Add(obj);
                amount = 0;
            }

            if (amount == 0) break;
        }

        var json = new PaybillDto
        {
            mc_request_id = payBillRequestLog.TransCode,
            service_code = serviceCode,
            publisher = publisher,
            customer_code = payBillRequestLog.ReceiverInfo,
            bill_payment = arrays.ToArray()
        }.ToJson();

        _logger.LogInformation($"{payBillRequestLog.TransCode} VimoConnector Param_Json: " + json);
        var encrypt = new VimoAes256().Encrypt(json, providerInfo.Password);
        var strCheckum = providerInfo.Username + encrypt + providerInfo.PublicKey;

        var request = new VimoRequest
        {
            fnc = "paybill",
            Merchantcode = providerInfo.Username,
            data = encrypt,
            Checksum = strCheckum.EncryptMd5()
        };

        var result = await CallApi(providerInfo, request, payBillRequestLog.TransCode);
        _logger.LogInformation($"{payBillRequestLog.TransCode} VimoConnector Topup Reponse: " + result.ToJson());

        try
        {
            if (result != null && result.error_code == ResponseCodeConst.Error)
            {
                payBillRequestLog.ModifiedDate = DateTime.Now;
                payBillRequestLog.ResponseInfo = result.ToJson();
                _logger.LogInformation(
                    $"VimoConnector return: {payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
                payBillRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
                await _cacheManager.ClearCache(key);
            }
            else
            {
                if (result != null && (providerInfo.ExtraInfo ?? "").Contains(result.error_code))
                {
                    payBillRequestLog.ModifiedDate = DateTime.Now;
                    payBillRequestLog.ResponseInfo = result.ToJson();
                    _logger.LogInformation(
                        $"VimoConnector return:{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
                    payBillRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch lỗi phía NCC";
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("VIMO",
                        result.error_code, payBillRequestLog.TransCode);
                    responseMessage.ResponseCode = reResult != null
                        ? reResult.ResponseCode
                        : ResponseCodeConst.ResponseCode_ErrorProvider;
                    responseMessage.ResponseMessage =
                        reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                }
                else
                {
                    _logger.LogInformation(
                        $"VimoConnector return:{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                    payBillRequestLog.Status = TransRequestStatus.Timeout;
                    payBillRequestLog.ModifiedDate = DateTime.Now;
                }
            }

            responseMessage.ProviderResponseCode = result?.error_code;
            responseMessage.ProviderResponseMessage = result?.error_message;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(
                $"VimoConnector Error: {payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()} Exception: {ex}");
            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
            payBillRequestLog.Status = TransRequestStatus.Timeout;
        }

        await _topupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);

        return responseMessage;
    }

    private async Task<VimoReponse> CallApi(ProviderInfoDto providerInfo, VimoRequest request, string transCode)
    {
        try
        {
            _logger.LogInformation($"{transCode}-{request.fnc} VimoConnector CallApi_Request: " + request.ToJson());

            var strbuilder = new StringBuilder();
            strbuilder.Append(providerInfo.ApiUser);
            strbuilder.Append(":");
            strbuilder.Append(providerInfo.ApiPassword);
            var strAuth = strbuilder.ToString();
            var auth = "Basic " + Convert.ToBase64String(strAuth.ToUtf8Bytes());
            var apiUrl = providerInfo.ApiUrl;
            var client = new RestClient(apiUrl);
            var requestDto = new RestRequest(request.fnc, Method.Post);
            requestDto.AddHeader("Accept", "application/x-www-form-urlencoded");
            requestDto.AddHeader("Authorization", auth);
            requestDto.AddParameter("fnc", request.fnc);
            requestDto.AddParameter("Merchantcode", request.Merchantcode);
            requestDto.AddParameter("data", request.data);
            requestDto.AddParameter("Checksum", request.Checksum);
            var response = await client.ExecuteAsync(requestDto);
            //_logger.LogInformation($"{transCode}-{request.fnc} VimoConnector CallApi_Reponse : " + response.ToJson());
            var responseContent = response.Content.FromJson<VimoReponse>();
            return responseContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"{transCode}-{request.fnc} VimoConnector CallApi_Exception : {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
        }

        return new VimoReponse();
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