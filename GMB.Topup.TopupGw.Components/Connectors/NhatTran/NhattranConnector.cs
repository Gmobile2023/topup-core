using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.TopupGw.Contacts.ApiRequests;
using GMB.Topup.TopupGw.Contacts.Dtos;
using GMB.Topup.TopupGw.Contacts.Enums;
using GMB.Topup.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace GMB.Topup.TopupGw.Components.Connectors.NhatTran;

public class NhattranConnector : IGatewayConnector
{
    private readonly ICacheManager _cacheManager;
    private readonly ILogger<NhattranConnector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;


    public NhattranConnector(ITopupGatewayService topupGatewayService,
        ICacheManager cacheManager,
        ILogger<NhattranConnector> logger)
    {
        _topupGatewayService = topupGatewayService;
        _cacheManager = cacheManager;
        _logger = logger;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        //_logger.Log(LogLevel.Information, "NhattranConnector request: " + topupRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        // var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(topupRequestLog.ProviderCode);
        //
        // if (providerInfo == null)
        // {
        //     _logger.LogInformation("providerInfo is null");
        //     return responseMessage;
        // }
        if (!_topupGatewayService.ValidConnector(ProviderConst.NHATTRAN, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-NhattranConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var request = new NhattranRequest
        {
            TransCode = topupRequestLog.TransCode,
            ReceiverNumber = topupRequestLog.ReceiverInfo,
            Amount = topupRequestLog.TransAmount,
            Telco = topupRequestLog.Vendor
        };

        responseMessage.TransCodeProvider = topupRequestLog.TransCode;

        if (providerInfo.PublicKey.ToUpper().StartsWith("TRUE"))
        {
            var checkMobile = await CheckMobile(providerInfo, request.ReceiverNumber);
            if (!checkMobile)
            {
                topupRequestLog.ResponseInfo = "Không phải số thuê bao viettel";
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
                await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
                responseMessage.ExtraInfo = "STOP";
                return responseMessage;
            }
        }

        var result = await CallNTApi(providerInfo, request);
        _logger.LogInformation($"NhattranConnector return: {topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
        var sendTelegram = false;
        var extra = (providerInfo.ExtraInfo ?? string.Empty).Split('|');
        var extraInfo = extra[0];
        var extraWarning = extra.Length >= 2 ? extra[1] : "";
        var content = "";
        try
        {
            responseMessage.ProviderResponseCode = result?.responseStatus.errorCode;
            responseMessage.ProviderResponseMessage = result?.responseStatus.message;
            if (result != null && result.responseStatus.errorCode == "00")
            {
                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.ResponseInfo = result.ToJson();
                topupRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
                responseMessage.ProviderResponseCode = result.responseStatus.transCode;
                responseMessage.ReceiverType = result.receiverType;
            }
            else if (result != null && result.responseStatus.errorCode != "00" &&
                     extraInfo.Contains(result.responseStatus.errorCode))
            {
                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.ResponseInfo = result.ToJson();
                //_logger.LogInformation($"NhattranConnector return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("NHATTRAN",
                    result.responseStatus.errorCode, topupRequestLog.TransCode);
                responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseMessage = reResult != null
                    ? reResult.ResponseName
                    : "Giao dịch không thành công từ nhà cung cấp";

                sendTelegram = true;
                content = result.ToJson();
                if (extraWarning.ToLower().Contains(result.responseStatus.errorCode.ToLower()))
                    sendTelegram = false;

                //Gunner. chỗ này k cần gán lại nữa. đã có phần convert mã lỗi ở trên
                // if (extra.Length >= 3 && extra[2].Contains(result.responseStatus.errorCode))
                //     responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_NotValidTelco;
            }
            else
            {
                _logger.LogInformation(
                    $"NhattranConnector return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                topupRequestLog.Status = TransRequestStatus.Timeout;
                topupRequestLog.ModifiedDate = DateTime.Now;

                content = result.ToJson();
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(
                $"NhattranConnector Error: {topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()} Exception: {ex}");
            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
            topupRequestLog.Status = TransRequestStatus.Timeout;
            responseMessage.Exception = ex.Message;
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
            _logger.LogInformation($"{transCodeToCheck}NhattranConnector CheckTrans request: " + transCode);
            var responseMessage = new MessageResponseBase();

            if (providerInfo == null)
                providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);


            if (providerInfo == null ||
                !_topupGatewayService.ValidConnector(ProviderConst.NHATTRAN, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{transCode}-{providerCode}-NhattranConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            var resultCheckTrans = await CallApiCheckTrans(providerInfo, transCodeToCheck);
            _logger.LogInformation($"{transCode} checkTranTopup return: {resultCheckTrans.ToJson()}");

            //responseMessage.ExtraInfo = string.Join("|", resultCheckTrans.responseStatus.errorCode, resultCheckTrans.responseStatus.message);
            if (resultCheckTrans.responseStatus.errorCode == "00")
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
                responseMessage.ProviderResponseCode = resultCheckTrans.responseStatus.transCode;
                responseMessage.ReceiverType = resultCheckTrans.receiverType;
            }
            else if (resultCheckTrans.responseStatus.errorCode == "01")
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = !string.IsNullOrEmpty(resultCheckTrans.responseStatus.message)
                    ? resultCheckTrans.responseStatus.message
                    : "Giao dịch không thành công";
            }
            else
            {
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }

            responseMessage.ProviderResponseCode = resultCheckTrans?.responseStatus.errorCode;
            responseMessage.ProviderResponseMessage = resultCheckTrans?.responseStatus.message;
            return responseMessage;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return new MessageResponseBase
            {
                ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                ResponseCode = ResponseCodeConst.ResponseCode_TimeOut
            };
        }
    }

    public async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
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

        if (!_topupGatewayService.ValidConnector(ProviderConst.NHATTRAN, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-NhattranConnector ProviderConnector not valid");
            responseMessage.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ");
            return responseMessage;
        }

        var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
        var telCo = string.Empty;
        if (providerService != null)
            telCo = providerService.ServiceCode;
        else
            _logger.LogWarning(
                $"{payBillRequestLog.TransCode} NhattranConnector QueryAsync ProductCode [{payBillRequestLog.ProductCode}] is null");


        var client = new JsonServiceClient(providerInfo.ApiUrl)
            {Timeout = TimeSpan.FromSeconds(providerInfo.Timeout)};

        try
        {
            var result = new NhattranReponse();
            _logger.LogInformation(
                $"{payBillRequestLog.ReceiverInfo} NhattranConnector check_phone_debit send: {payBillRequestLog.ReceiverInfo}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}");
            if (telCo.StartsWith("EVN") || telCo.StartsWith("NUOC"))
            {
                var sv = telCo.Split('|');
                result = telCo.StartsWith("EVN")
                    ? client.Get<NhattranReponse>(
                        $"/api/v1/ngate/bill/{payBillRequestLog.ReceiverInfo}?ServiceCode={sv[0]}")
                    : client.Get<NhattranReponse>(
                        $"/api/v1/ngate/bill/{payBillRequestLog.ReceiverInfo}?ServiceCode={sv[0]}&WaterCode={sv[1]}");
                _logger.LogInformation(
                    $"{payBillRequestLog.ReceiverInfo} NhattranConnector Bill Reponse: {providerInfo.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
            }
            else if (telCo.StartsWith("FINANCE"))
            {
                var sv = telCo.Split('|');
                result = client.Get<NhattranReponse>(
                    $"/api/v1/ngate/finance/{payBillRequestLog.ReceiverInfo}?ServiceCode={sv[1]}");
                _logger.LogInformation(
                    $"{payBillRequestLog.ReceiverInfo} NhattranConnector Bill Reponse: {providerInfo.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
            }
            else
            {
                result = client.Get<NhattranReponse>(
                    $"/api/v1/ngate/check_phone_debit?CustomerCode={payBillRequestLog.ReceiverInfo}&telCo={telCo}");
                _logger.LogInformation(
                    $"{payBillRequestLog.ReceiverInfo} NhattranConnector check_phone_debit Reponse: {providerInfo.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
            }

            if (result != null && result.responseStatus.errorCode == "00")
            {
                var res = (result.Results ?? string.Empty).Split('|');
                var lst = new List<PeriodDto>();
                if (telCo.StartsWith("EVN") || telCo.StartsWith("NUOC") || telCo.StartsWith("FINANCE"))
                {
                    if (decimal.Parse(res[0]) > 0 && res.Length >= 3)
                        if (telCo.StartsWith("EVN"))
                        {
                            var ky = res[3].Split(';');
                            if (ky.Length >= 3)
                            {
                                if (ky[2].Split('/').Length == 2)
                                {
                                    lst.Add(new PeriodDto
                                    {
                                        Period = ky[2],
                                        Amount = decimal.Parse(res[0])
                                    });
                                }
                                else if (ky[2].Split(':').Length == 2)
                                {
                                    var k = ky[2].Split(':')[1].Split('-');
                                    lst.Add(new PeriodDto
                                    {
                                        Period = k[1] + "/" + k[0],
                                        Amount = decimal.Parse(res[0])
                                    });
                                }
                            }
                        }

                    var keyBill = new BillinfoDto
                    {
                        OriginalId = res.Length >= 2 ? res[1] : string.Empty,
                        BenCustomerName = res.Length >= 3 ? res[2] : string.Empty,
                        BenInfo = res.Length >= 4 ? res[3] : string.Empty,
                        ServiceIndicator = res.Length >= 5 ? res[4] : string.Empty,
                        TidNumber = res.Length >= 6 ? res[5] : string.Empty
                    };

                    var key =
                        $"PayGate_BillQuery:Items:{payBillRequestLog.ReceiverInfo}_{payBillRequestLog.ProviderCode}";
                    await _cacheManager.AddEntity(key, keyBill, TimeSpan.FromMinutes(15));
                }

                var dto = telCo.StartsWith("EVN") || telCo.StartsWith("NUOC") || telCo.StartsWith("FINANCE")
                    ? new InvoiceResultDto
                    {
                        Amount = decimal.Parse(res[0]),
                        CustomerName = res.Length >= 3 ? res[2] : string.Empty,
                        Address = string.Empty,
                        BillType = string.Empty,
                        Period = lst.Count() >= 1 ? lst.First().Period : string.Empty,
                        PeriodDetails = lst
                    }
                    : new InvoiceResultDto
                    {
                        Amount = decimal.Parse(res[0]),
                        CustomerName = res.Length >= 2 ? res[1] : string.Empty,
                        PeriodDetails = lst
                    };

                responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Success;
                responseMessage.ResponseStatus.Message = "Giao dịch thành công";
                responseMessage.Results = dto;
            }
            else
            {
                responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Error;
                responseMessage.ResponseStatus.Message = "Truy vấn thất bại";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{payBillRequestLog.ReceiverInfo} QueryAsync .Exception: " + ex.Message);

            responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Error;
            responseMessage.ResponseStatus.Message = "Truy vấn thất bại.";
        }

        return responseMessage;
    }

    public Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        _logger.Log(LogLevel.Information, "QueryBalanceAsync request: " + providerCode);
        var responseMessage = new MessageResponseBase();
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

        if (providerInfo == null)
        {
            _logger.LogInformation("providerInfo is null");
            return responseMessage;
        }

        if (!_topupGatewayService.ValidConnector(ProviderConst.NHATTRAN, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{providerCode}-{transCode}-{providerInfo.ProviderCode}-NhattranConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var request = new CheckBalanceRequest
        {
            Command = "CheckTotalBalance"
        };
        try
        {
            //_logger.LogInformation($"{providerCode} kpp_control send: " + request.ToJson());
            var result = await HttpHelper.Post<NhattranReponse, CheckBalanceRequest>(providerInfo.ApiUrl,
                "/api/v1/ngate/kpp_control", request, timeout: TimeSpan.FromSeconds(providerInfo.Timeout));
            _logger.LogInformation($"{providerCode} kpp_control Reponse: {result.ToJson()}");

            if (result != null && result.responseStatus.errorCode == "00")
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                responseMessage.Payload = result.Results;
            }
            else
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Truy vấn thất bại";
                responseMessage.Payload = "0";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{providerCode} QueryBalanceAsync .Exception: " + ex.Message);

            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Truy vấn thất bại.";
            responseMessage.Payload = "0";
        }

        return responseMessage;
    }

    public Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
    {
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        //_logger.Log(LogLevel.Information, "NhattranConnector request: " + payBillRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

        if (providerInfo == null)
        {
            _logger.LogInformation("providerInfo is null");
            return responseMessage;
        }

        var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
        var telCo = string.Empty;
        if (providerService != null)
            telCo = providerService.ServiceCode;
        else
            _logger.LogWarning(
                $"{payBillRequestLog.TransCode} NhattranConnector request with ProductCode [{payBillRequestLog.ProductCode}] is null");


        if (!_topupGatewayService.ValidConnector(ProviderConst.NHATTRAN, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-NhattranConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var request = new NhattranRequest();
        var requestBill = new NhattranBillRequest();
        var paymentType = 0;
        if (telCo.StartsWith("NUOC") || telCo.StartsWith("EVN") || telCo.StartsWith("FINANCE"))
        {
            paymentType = 1;
            var sv = telCo.Split('|');
            var key = $"PayGate_BillQuery:Items:{payBillRequestLog.ReceiverInfo}_{payBillRequestLog.ProviderCode}";
            var keyBill = await _cacheManager.GetEntity<BillinfoDto>(key);
            if (keyBill == null)
            {
                keyBill = await QueryBillAsync(providerInfo.ApiUrl, telCo, payBillRequestLog.ReceiverInfo);
                if (keyBill == null)
                {
                    _logger.LogInformation(
                        $"{payBillRequestLog.TransCode} NhattranConnector Paybill cannot get QueryInfo");
                    responseMessage.ResponseMessage = "Giao dịch không thành công. Vui lòng thử lại sau";
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    payBillRequestLog.Status = TransRequestStatus.Fail;
                    return responseMessage;
                }
            }

            if (telCo.StartsWith("FINANCE"))
            {
                paymentType = 2;
                requestBill = new NhattranBillRequest
                {
                    TransCode = payBillRequestLog.TransCode,
                    BillNo = payBillRequestLog.ReceiverInfo,
                    Amount = Convert.ToInt32(payBillRequestLog.TransAmount),
                    ServiceCode = sv[1],
                    WaterCode = "",
                    BenInfo = keyBill.BenInfo,
                    OriginalId = keyBill.OriginalId,
                    ServiceIndicator = keyBill.ServiceIndicator,
                    TidNumber = keyBill.TidNumber
                };
            }
            else
            {
                requestBill = new NhattranBillRequest
                {
                    TransCode = payBillRequestLog.TransCode,
                    BillNo = payBillRequestLog.ReceiverInfo,
                    Amount = Convert.ToInt32(payBillRequestLog.TransAmount),
                    ServiceCode = sv[0],
                    WaterCode = sv.Length >= 2 ? sv[1] : string.Empty,
                    BenInfo = keyBill.BenInfo,
                    OriginalId = keyBill.OriginalId,
                    ServiceIndicator = keyBill.ServiceIndicator,
                    TidNumber = keyBill.TidNumber
                };
            }
        }
        else
        {
            request = new NhattranRequest
            {
                TransCode = payBillRequestLog.TransCode,
                ReceiverNumber = payBillRequestLog.ReceiverInfo,
                Amount = Convert.ToInt32(payBillRequestLog.TransAmount),
                Telco = telCo
            };
        }


        responseMessage.TransCodeProvider = payBillRequestLog.TransCode;

        var result = paymentType == 0 ? await CallNTApi(providerInfo, request)
            : paymentType == 2 ? await CallBillTieuDungApi(providerInfo, requestBill)
            : await CallBillApi(providerInfo, requestBill);
        var extra = (providerInfo.ExtraInfo ?? string.Empty).Split('|');
        var extraInfo = extra[0];
        try
        {
            responseMessage.ProviderResponseCode = result?.responseStatus.errorCode;
            responseMessage.ProviderResponseMessage = result?.responseStatus.message;
            if (result != null && result.responseStatus.errorCode == "00")
            {
                payBillRequestLog.ModifiedDate = DateTime.Now;
                payBillRequestLog.ResponseInfo = result.ToJson();
                _logger.LogInformation(
                    $"NhattranConnector return: {payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
                payBillRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
                responseMessage.ProviderResponseCode = result.responseStatus.transCode;
                responseMessage.ReceiverType = result.receiverType;
            }
            else if (result != null && result.responseStatus.errorCode != "00" &&
                     extraInfo.Contains(result.responseStatus.errorCode))
            {
                payBillRequestLog.ModifiedDate = DateTime.Now;
                payBillRequestLog.ResponseInfo = result.ToJson();
                _logger.LogInformation(
                    $"NhattranConnector return:{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
                payBillRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("NHATTRAN",
                    result.responseStatus.errorCode, payBillRequestLog.TransCode);
                responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseMessage = reResult != null
                    ? reResult.ResponseName
                    : "Giao dịch không thành công từ nhà cung cấp";
            }
            else
            {
                _logger.LogInformation( $"NhattranConnector return:{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                payBillRequestLog.Status = TransRequestStatus.Timeout;
                payBillRequestLog.ModifiedDate = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"NhattranConnector Error: {payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()} Exception: {ex}");
            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
            payBillRequestLog.Status = TransRequestStatus.Timeout;
            responseMessage.Exception = ex.Message;
        }

        await _topupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);
        return responseMessage;
    }

    private Task<BillinfoDto> QueryBillAsync(string apiUrl, string telCo, string receiverInfo)
    {
        var client = new JsonServiceClient(apiUrl)
            {Timeout = TimeSpan.FromSeconds(60)};

        try
        {
            var sv = telCo.Split('|');
            _logger.LogInformation($"{receiverInfo} NhattranConnector Bill send: {receiverInfo}");
            var result = telCo.StartsWith("FINANCE")
                ? client.Get<NhattranReponse>($"/api/v1/ngate/finance/{receiverInfo}?ServiceCode={sv[1]}")
                : telCo.StartsWith("EVN")
                    ? client.Get<NhattranReponse>($"/api/v1/ngate/bill/{receiverInfo}?ServiceCode={sv[0]}")
                    : client.Get<NhattranReponse>(
                        $"/api/v1/ngate/bill/{receiverInfo}?ServiceCode={sv[0]}&WaterCode={sv[1]}");
            _logger.LogInformation($"{receiverInfo} NhattranConnector Bill Reponse: {result.ToJson()}");

            if (result != null && result.responseStatus.errorCode == "00")
            {
                var res = (result.Results ?? string.Empty).Split('|');
                var dto = new BillinfoDto
                {
                    OriginalId = res.Length >= 2 ? res[1] : string.Empty,
                    BenCustomerName = res.Length >= 3 ? res[2] : string.Empty,
                    BenInfo = res.Length >= 4 ? res[3] : string.Empty,
                    ServiceIndicator = res.Length >= 5 ? res[4] : string.Empty,
                    TidNumber = res.Length >= 6 ? res[5] : string.Empty
                };

                return Task.FromResult(dto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{receiverInfo} QueryAsync Bill .Exception: " + ex.Message);
        }

        return Task.FromResult<BillinfoDto>(null);
    }

    private async Task<NhattranReponse> CallNTApi(ProviderInfoDto providerInfo, NhattranRequest request)
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                BaseAddress = new Uri(providerInfo.ApiUrl)
            };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var data = new StringContent(request.ToJson(), Encoding.UTF8, "application/json");
            _logger.LogInformation($"{request.TransCode} NhattranConnector send: " + request.ToJson());
            var result = await client.PostAsync("/api/v1/ngate/topup", data);
            if (result.IsSuccessStatusCode)
            {
                var rs = await result.Content.ReadAsStringAsync();
                var getRs = rs.FromJson<NhattranReponse>();
                //_logger.LogInformation($"{request.TransCode} NhattranConnector Reponse: {getRs.ToJson()}");
                return getRs;
            }

            return new NhattranReponse
            {
                responseStatus = new ResponseStatus
                {
                    errorCode = "501102",
                    message = "Chưa check được trạng thái bên NCC!"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.TransCode} NhattranConnector Error: " + ex);
            return new NhattranReponse
            {
                responseStatus = new ResponseStatus
                {
                    errorCode = "501102",
                    message = ex.Message
                },
                ViewMessager = ex.Message
            };
        }
    }

    private async Task<NhattranReponse> CallBillApi(ProviderInfoDto providerInfo, NhattranBillRequest request)
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                BaseAddress = new Uri(providerInfo.ApiUrl)
            };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var data = new StringContent(request.ToJson(), Encoding.UTF8, "application/json");
            _logger.LogInformation($"{request.TransCode} bill NhattranConnector send: " + request.ToJson());
            var result = await client.PostAsync($"/api/v1/ngate/bill/{request.BillNo}", data);
            _logger.LogInformation($"{request.TransCode} NhattranConnector Reponse:{result.StatusCode}");
            if (result.IsSuccessStatusCode)
            {
                var rs = await result.Content.ReadAsStringAsync();
                var getRs = rs.FromJson<NhattranReponse>();
                //_logger.LogInformation($"{request.TransCode} bill NhattranConnector Reponse: {getRs.ToJson()}");
                return getRs;
            }

            return new NhattranReponse
            {
                responseStatus = new ResponseStatus
                {
                    errorCode = "501102",
                    message = "Chưa check được trạng thái bên NCC!"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.TransCode} bill NhattranConnector Error: " + ex);
            return new NhattranReponse
            {
                responseStatus = new ResponseStatus
                {
                    errorCode = "501102",
                    message = ex.Message
                },
                ViewMessager = ex.Message
            };
        }
    }

    private async Task<NhattranReponse> CallBillTieuDungApi(ProviderInfoDto providerInfo, NhattranBillRequest request)
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                BaseAddress = new Uri(providerInfo.ApiUrl)
            };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var data = new StringContent(request.ToJson(), Encoding.UTF8, "application/json");
            _logger.LogInformation(
                $"{request.TransCode} bill NhattranConnector Bill_TieuDung send: " + request.ToJson());
            var result = await client.PostAsync($"/api/v1/ngate/finance/{request.BillNo}", data);
            _logger.LogInformation($"{request.TransCode} NhattranConnector BillTieuDung Reponse:{result.StatusCode}");
            if (result.IsSuccessStatusCode)
            {
                var rs = await result.Content.ReadAsStringAsync();
                var getRs = rs.FromJson<NhattranReponse>();
                _logger.LogInformation(
                    $"{request.TransCode} bill NhattranConnector Bill_TieuDung Reponse: {getRs.ToJson()}");
                return getRs;
            }

            return new NhattranReponse
            {
                responseStatus = new ResponseStatus
                {
                    errorCode = "501102",
                    message = "Chưa check được trạng thái bên NCC!"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.TransCode} bill NhattranConnector Bill_TieuDung Error: " + ex);
            return new NhattranReponse
            {
                responseStatus = new ResponseStatus
                {
                    errorCode = "501102",
                    message = ex.Message
                },
                ViewMessager = ex.Message
            };
        }
    }

    private async Task<NhattranReponse> CallApiCheckTrans(ProviderInfoDto providerInfo, string transCode)
    {
        try
        {
            _logger.LogInformation($"{transCode} check_trans send: {transCode}");
            var result = await HttpHelper.Get<NhattranReponse>(providerInfo.ApiUrl,
                $"/api/v1/ngate/check_trans?TransCode={transCode}", TimeSpan.FromSeconds(providerInfo.Timeout));
            _logger.LogInformation(
                $"{transCode} CallApiCheckTrans Reponse: {providerInfo.ProviderCode}-{result.ToJson()}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{transCode} NhattranConnectorCheckTrans .Exception: " + ex);
            return new NhattranReponse
            {
                responseStatus = new ResponseStatus
                {
                    errorCode = "501102",
                    message = ex.Message
                },
                ViewMessager = ex.Message
            };
        }
    }

    private async Task<bool> CheckMobile(ProviderInfoDto providerInfo, string mobile)
    {
        var client = new JsonServiceClient(providerInfo.ApiUrl)
            {Timeout = TimeSpan.FromSeconds(providerInfo.Timeout)};

        try
        {
            _logger.LogInformation($"{mobile} check_phone_provider send: {mobile}");
            var result =
                await client.GetAsync<NhattranReponse>($"/api/v1/ngate/check_phone_provider?MobileNumber={mobile}");
            _logger.LogInformation(
                $"{mobile} check_phone_provider Reponse: {providerInfo.ProviderCode}-{result.ToJson()}");

            return result != null && result.Results == "VT" && result.responseStatus.errorCode == "00";
        }
        catch (Exception ex)
        {
            _logger.LogError($"{mobile} CheckMobile .Exception: " + ex);
            return false;
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

internal class NhattranRequest
{
    public string Telco { get; set; }
    public string ReceiverNumber { get; set; }
    public string TransCode { get; set; }
    public int Amount { get; set; }
}

internal class NhattranBillRequest
{
    public string TransCode { get; set; }
    public string ServiceCode { get; set; }
    public string BillNo { get; set; }
    public string WaterCode { get; set; }
    public string TidNumber { get; set; }
    public string BenInfo { get; set; }
    public string ServiceIndicator { get; set; }
    public string OriginalId { get; set; }
    public decimal Amount { get; set; }
}

internal class BillinfoDto
{
    public string TidNumber { get; set; }
    public string BenInfo { get; set; }
    public string ServiceIndicator { get; set; }
    public string BenCustomerName { get; set; }
    public string OriginalId { get; set; }
}

internal class CheckBalanceRequest
{
    public string Command { get; set; }
}

internal class NhattranReponse
{
    public string Results { get; set; }

    public string receiverType { get; set; }

    public ResponseStatus responseStatus { get; set; }

    public string ViewMessager { get; set; }
}

internal class ResponseStatus
{
    public string errorCode { get; set; }

    public string message { get; set; }

    public string transCode { get; set; }
}