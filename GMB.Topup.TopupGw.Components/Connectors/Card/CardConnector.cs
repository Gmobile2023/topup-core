using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.Shared.Helpers;
using GMB.Topup.Shared.Utils;
using GMB.Topup.TopupGw.Contacts.ApiRequests;
using GMB.Topup.TopupGw.Contacts.Dtos;
using GMB.Topup.TopupGw.Contacts.Enums;
using GMB.Topup.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace GMB.Topup.TopupGw.Components.Connectors.Card;

public class CardConnector : IGatewayConnector
{
    private readonly ICacheManager _cacheManager;
    private readonly ILogger<CardConnector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;


    public CardConnector(ITopupGatewayService topupGatewayService,
        ILogger<CardConnector> logger, ICacheManager cacheManager)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
        _cacheManager = cacheManager;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        _logger.Log(LogLevel.Information, "123CardConnector Topup request: " + topupRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        try
        {
            if (!_topupGatewayService.ValidConnector(ProviderConst.CARD, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-123CardConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var request = new CardRequest
            {
                PartnerCode = providerInfo.Username,
                TransCode = topupRequestLog.TransCode,
                Mobile = topupRequestLog.ReceiverInfo,
                Amount = topupRequestLog.TransAmount.ToString(),
                PaidType = 1,
                ServiceType = 1,
                IsSplitAmount = 0,
                Action = "CREATE"
            };

            if ((topupRequestLog.ProviderSetTransactionTimeout ?? 0) > 0)
                request.TimeoutInSec = topupRequestLog.ProviderSetTransactionTimeout;

            responseMessage.TransCodeProvider = topupRequestLog.TransCode;

            if (topupRequestLog.Vendor == "VTE")
                request.TelcoId = 1;
            if (topupRequestLog.Vendor == "VNA")
                request.TelcoId = 3;

            var result = await CallApi(providerInfo, request);

            responseMessage.Exception = result.ResponseMessage;
            responseMessage.ProviderResponseCode = result.ResponseCode.ToString();
            responseMessage.ProviderResponseMessage = result.ResponseMessage;
            if (new[] { 1, 0, 11, 13, 501102, 107 }.Contains(result.ResponseCode))
            {
                _logger.Log(LogLevel.Information, $"{topupRequestLog.TransCode} - Tạo giao dịch thành công");

                _logger.Log(LogLevel.Information,
                    $"{topupRequestLog.TransCode} - Đợi kết quả xử lý xong sau tối đa : {topupRequestLog.ProviderMaxWaitingTimeout} s");
                var startTime = DateTime.Now;
                responseMessage = await ProcessTopupAsync(topupRequestLog, providerInfo, request, responseMessage);
                var endTime = DateTime.Now;
                _logger.Log(LogLevel.Information,
                    $"{topupRequestLog.TransCode} - đã chờ kết quả trong {(endTime - startTime).TotalSeconds} s ==> {responseMessage.ToJson()}");
            }
            else
            {
                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.ResponseInfo = result.ToJson();
                _logger.LogInformation(
                    $"123CardConnector Topup fail return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                var reResult =
                    await _topupGatewayService.GetResponseMassageCacheAsync("123Card", "2", providerInfo.ProviderCode);
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseMessage =
                    reResult != null ? reResult.ResponseName : "Giao dịch không thành công từ nhà cung cấp";
                topupRequestLog.ModifiedDate = DateTime.Now;
            }

            _logger.Log(LogLevel.Information, $"{topupRequestLog.TransCode} ==> {responseMessage.ToJson()}");
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

    private async Task<MessageResponseBase> ProcessTopupAsync(TopupRequestLogDto topupRequestLog,
        ProviderInfoDto providerInfo, CardRequest request, MessageResponseBase responseMessage)
    {
        double timeRecound = 0;
        var key = $"PayGate_TopupRequest:Items:{string.Join("_", topupRequestLog.ProviderCode, request.TransCode)}";
        var begin = DateTime.Now;
        Thread.Sleep(3000);

        request.Action = "CHECK";
        var checkResult = await CallApi(providerInfo, request);
        var noQuery = 1;
        while (new[] { 0, 11, 13, 501102 }.Contains(checkResult.ResponseCode))
        {
            if (checkResult.ResponseCode == 501102 || checkResult.ResponseCode == 13)
            {
                var timmeQuery = await _topupGatewayService.GetTopupRequestLogAsync(topupRequestLog.TransRef,
                    topupRequestLog.ProviderCode);
                if (timmeQuery != null && (timmeQuery.Status == TransRequestStatus.Success ||
                                           timmeQuery.Status == TransRequestStatus.Fail))
                {
                    _logger.Log(LogLevel.Information, "Get TopupLog reponse : " + timmeQuery.ToJson());
                    checkResult = new CardReponse
                    {
                        ResponseCode = timmeQuery.Status == TransRequestStatus.Success ? 1 : 2,
                        ResponseMessage = "Ket qua tu callBack",
                        TotalTopupAmount = Convert.ToInt32(timmeQuery.TransAmount)
                    };

                    break;
                }
            }

            Thread.Sleep(1000);
            var responseCache = await _cacheManager.GetEntity<TopupRequestLogDto>(key);
            if (responseCache != null)
                if (responseCache.Status == TransRequestStatus.Success ||
                    responseCache.Status == TransRequestStatus.Fail)
                {
                    checkResult.ResponseCode = responseCache.Status == TransRequestStatus.Success ? 1 : 2;
                    checkResult.TransCode = responseCache.TransCode;
                    checkResult.ResponseMessage = "Ket qua tu cache callBack_NCC";
                    checkResult.TotalTopupAmount = Convert.ToInt32(responseCache.TransAmount);
                    _logger.LogInformation(request.TransCode +
                                           $"123CardConnector Lay_ket_qua_tu cache: {checkResult.ToJson()}");
                }

            if (new[] { 1, 2 }.Contains(checkResult.ResponseCode))
            {
                break;
            }
            else
            {
                request.Action = "CHECK";
                checkResult = await CallApi(providerInfo, request);
            }

            var end = DateTime.Now;
            noQuery++;
            if (end.Subtract(begin).TotalSeconds > (topupRequestLog.ProviderMaxWaitingTimeout ?? providerInfo.Timeout))
            {
                if (checkResult.ResponseCode == 0)
                {
                    request.Action = "CANCEL";
                    var cancelResult = await CallApi(providerInfo, request);
                    _logger.LogInformation(request.TransCode +
                                           $"123CardConnector CancelTopupReturn: {cancelResult.ToJson()}");
                    request.Action = "CHECK";
                    checkResult = await CallApi(providerInfo, request);
                }
                else if (new[] { 11, 13, 501102 }.Contains(checkResult.ResponseCode))
                {
                    var timmeQuery = await _topupGatewayService.GetTopupRequestLogAsync(
                        topupRequestLog.TransRef,
                        topupRequestLog.ProviderCode);
                    if (timmeQuery != null && (timmeQuery.Status == TransRequestStatus.Success ||
                                               timmeQuery.Status == TransRequestStatus.Fail))
                    {
                        _logger.Log(LogLevel.Information, "Get TopupLog reponse : " + timmeQuery.ToJson());
                        checkResult = new CardReponse
                        {
                            ResponseCode = timmeQuery.Status == TransRequestStatus.Success ? 1 : 2,
                            ResponseMessage = "Ket qua tu callBack",
                            TotalTopupAmount = Convert.ToInt32(timmeQuery.TransAmount)
                        };
                    }
                }

                timeRecound = end.Subtract(begin).TotalSeconds;
                break;
            }
        }

        _logger.LogInformation(request.TransCode +
                               $"123CardConnector TopupReturn: {topupRequestLog.ProviderCode}-{checkResult.ToJson()}");

        try
        {
            responseMessage.ProviderResponseCode = checkResult?.ResponseCode.ToString();
            responseMessage.ProviderResponseMessage = checkResult?.ResponseMessage;
            if (checkResult.ResponseCode == 1)
            {
                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.TransAmount = checkResult.TotalTopupAmount;
                topupRequestLog.ResponseInfo = checkResult.ToJson();
                _logger.LogInformation(
                    $"123CardConnector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{checkResult.ToJson()}");
                topupRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
                responseMessage.ExtraInfo = checkResult.TotalTopupAmount.ToString();
            }
            else if (new[] { 10, 2, 100, 20 }.Contains(checkResult.ResponseCode))
            {
                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.ResponseInfo = checkResult.ToJson();
                _logger.LogInformation(
                    $"123CardConnector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{checkResult.ToJson()}");
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("123Card",
                    checkResult.ResponseCode.ToString(), providerInfo.ProviderCode);
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode =
                    reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseMessage = reResult != null
                    ? reResult.ResponseName
                    : "Giao dịch không thành công từ nhà cung cấp";
            }
            else if (checkResult.ResponseCode == 501102)
            {
                _logger.LogInformation(
                    $"123CardConnector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{checkResult.ToJson()}");
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                topupRequestLog.Status = TransRequestStatus.Timeout;
                topupRequestLog.TopupGateTimeOut = ResponseCodeConst.ResponseCode_TimeOut;
                responseMessage.ProviderResponseCode = "501102";
                responseMessage.ProviderResponseMessage = checkResult.ResponseMessage;
            }
            else
                _logger.LogInformation(
                    $"123CardConnector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{checkResult.ToJson()}");
        }
        catch (Exception ex)
        {
            _logger.LogInformation(
                $"123CardConnector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{checkResult} Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
            topupRequestLog.Status = TransRequestStatus.Timeout;
            topupRequestLog.TopupGateTimeOut = ResponseCodeConst.ResponseCode_TimeOut;
            responseMessage.Exception = ex.Message;
            responseMessage.ProviderResponseCode = "501102";
            responseMessage.ProviderResponseMessage = ex.Message;
        }
        finally
        {
            await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
            await _cacheManager.ClearCache(key);   
        }
        return responseMessage;
    }

    public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
    {
        try
        {
            _logger.LogInformation($"{transCodeToCheck}-123CardConnector Check request: " + transCode);
            var responseMessage = new MessageResponseBase();

            if (providerInfo == null)
                providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null ||
                !_topupGatewayService.ValidConnector(ProviderConst.CARD, providerInfo.ProviderCode))
            {
                _logger.LogError($"{transCode}-{providerCode}-123CardConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            var request = new CardRequest
            {
                PartnerCode = providerInfo.Username,
                TransCode = transCodeToCheck,
                Mobile = "",
                PaidType = 1,
                ServiceType = 1,
                IsSplitAmount = 0,
                Action = "CHECK"
            };

            _logger.LogInformation($"{transCodeToCheck}123CardConnector CheckTrans  send: " + request.ToJson());

            var checkResult = await CallApi(providerInfo, request);
            _logger.LogInformation(
                $"{providerCode}-{transCodeToCheck}123CardConnector CheckTopup return: {checkResult.ToJson()}");

            if (checkResult.ResponseCode == 1)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
            }
            else if (new[] { 10, 2, 100 }.Contains(checkResult.ResponseCode))
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
            }
            else
            {
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
            }

            responseMessage.ProviderResponseCode = checkResult?.ResponseCode.ToString();
            responseMessage.ProviderResponseMessage = checkResult?.ResponseMessage;
            return responseMessage;
        }
        catch (Exception ex)
        {
            return new MessageResponseBase
            {
                ResponseMessage = ex.Message,
                ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                ProviderResponseCode = "501102",
                ProviderResponseMessage = ex.Message
            };
        }
    }

    public async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
        return await Task.FromResult(new NewMessageResponseBase<InvoiceResultDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Nhà cung cấp không hỗ trợ truy vấn")
        });
    }

    public Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        return await Task.FromResult(new MessageResponseBase());
    }

    public Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
    {
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.Log(LogLevel.Information, "Get topup request: " + payBillRequestLog.ToJson());

        if (!_topupGatewayService.ValidConnector(ProviderConst.CARD, payBillRequestLog.ProviderCode))
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };

        var responseMessage = new MessageResponseBase();

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

        if (providerInfo == null)
        {
            _logger.LogInformation("providerInfo is null");
            return responseMessage;
        }

        payBillRequestLog.TransCode = providerInfo.Username + "_" + payBillRequestLog.TransCode;
        await _topupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);

        var request = new CardRequest
        {
            PartnerCode = providerInfo.Username,
            TransCode = payBillRequestLog.TransCode,
            Mobile = payBillRequestLog.ReceiverInfo,
            Amount = payBillRequestLog.TransAmount.ToString(),
            PaidType = 2,
            ServiceType = 1,
            IsSplitAmount = 0,
            Action = "CREATE",
        };

        responseMessage.TransCodeProvider = payBillRequestLog.TransCode;
        if (payBillRequestLog.Vendor == "VTE")
            request.TelcoId = 1;
        if (payBillRequestLog.Vendor == "VNA")
            request.TelcoId = 3;


        var result = await CallApi(providerInfo, request);
        if (new[] { 1, 0, 11, 13, 501102, 107 }.Contains(result.ResponseCode))
        {
            var begin = DateTime.Now;
            Thread.Sleep(3000);
            request.Action = "CHECK";
            var checkResult = await CallApi(providerInfo, request);
            var noQuery = 1;
            while (new[] { 0, 11, 13, 501102 }.Contains(checkResult.ResponseCode))
            {
                Thread.Sleep(1000);
                checkResult = await CallApi(providerInfo, request);
                var end = DateTime.Now;
                _logger.LogInformation("Query Request {0}, status {3}. times {1}, waiting time {2} ms",
                    request.TransCode, noQuery,
                    end.Subtract(begin).TotalSeconds, checkResult.ResponseCode);
                noQuery++;
                if (end.Subtract(begin).TotalSeconds > providerInfo.Timeout)
                {
                    if (checkResult.ResponseCode == 0)
                    {
                        request.Action = "CANCEL";
                        var cancelResult = await CallApi(providerInfo, request);
                        _logger.LogInformation(request.TransCode + $" CancelTopupReturn: {cancelResult.ToJson()}");
                        request.Action = "CHECK";
                        checkResult = await CallApi(providerInfo, request);
                    }

                    break;
                }
            }

            _logger.LogInformation(request.TransCode + $" TopupReturn: {checkResult.ToJson()}");

            try
            {
                if (checkResult.ResponseCode == 1)
                {
                    payBillRequestLog.ModifiedDate = DateTime.Now;
                    payBillRequestLog.TransAmount = checkResult.TotalTopupAmount;
                    payBillRequestLog.ResponseInfo = checkResult.ToJson();
                    _logger.LogInformation(
                        $"Topup return:{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{checkResult}");
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync("123Card", "1",
                            providerInfo.ProviderCode);
                    payBillRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Thành công";
                    responseMessage.ExtraInfo = checkResult.TotalTopupAmount.ToString();
                }
                else if (new[] { 10, 2, 100 }.Contains(checkResult.ResponseCode))
                {
                    payBillRequestLog.ModifiedDate = DateTime.Now;
                    payBillRequestLog.ResponseInfo = result.ToJson();
                    _logger.LogInformation(
                        $"Topup return:{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{checkResult}");
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync("123Card", "2",
                            providerInfo.ProviderCode);
                    payBillRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null
                        ? reResult.ResponseName
                        : "Giao dịch không thành công từ nhà cung cấp";
                }
                else
                {
                    _logger.LogInformation(
                        $"Topup return:{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{checkResult}");
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                    payBillRequestLog.Status = TransRequestStatus.Timeout;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(
                    $"Topup return: {payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{checkResult} Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                payBillRequestLog.Status = TransRequestStatus.Timeout;
            }
        }
        else
        {
            payBillRequestLog.ModifiedDate = DateTime.Now;
            payBillRequestLog.ResponseInfo = result.ToJson();
            _logger.LogInformation(
                $"Topup return:{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result}");
            var reResult =
                await _topupGatewayService.GetResponseMassageCacheAsync("123Card", "2", providerInfo.ProviderCode);
            payBillRequestLog.Status = TransRequestStatus.Fail;
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage =
                reResult != null ? reResult.ResponseName : "Giao dịch không thành công từ nhà cung cấp";
        }

        await _topupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);

        return responseMessage;
    }

    private async Task<CardReponse> CallApi(ProviderInfoDto providerInfo, CardRequest request)
    {
        var client = new JsonHttpClient(providerInfo.ApiUrl)
        {
            HttpClient = new HttpClient
                { Timeout = TimeSpan.FromSeconds(providerInfo.Timeout), BaseAddress = new Uri(providerInfo.ApiUrl) }
        };

        try
        {
            request.Signature = (request.TransCode + request.PartnerCode + providerInfo.Password)
                .EncryptMd5().ToLower();
            if (request.Action == "CREATE")
                _logger.LogInformation(
                    $"{request.TransCode}|{request.Action}|TimeOutWait:{request.TimeoutInSec} 123CardConnector Topup send: {request.ToJson()}");
            else
                Console.WriteLine(
                    $"{request.TransCode}|{request.Action}|TimeOutWait:{request.TimeoutInSec} 123CardConnector Topup send: {request.TransCode}-{request.TimeoutInSec}");

            var result = await client.PostAsync<CardReponse>("/TopupOrder", request);
            if (result?.ResponseCode != 11)
                _logger.LogInformation(
                    $"{request.TransCode}|{request.Action} 123CardConnector Topup Response: {result.ToJson()}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.TransCode}|{request.Action} 123CardConnector Topup Exception: " + ex);
            return new CardReponse
            {
                ResponseCode = 501102, //Tự quy định mã này cho trường hợp timeout.
                ResponseMessage = ex.Message
            };
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

public class CardRequest
{
    public string PartnerCode { get; set; }
    public string TransCode { get; set; }
    public string Mobile { get; set; }
    public string Amount { get; set; }
    public int TelcoId { get; set; }
    public int PaidType { get; set; }
    public int ServiceType { get; set; }
    public int IsSplitAmount { get; set; }
    public string Action { get; set; }

    public string Signature { get; set; }
    public int? TimeoutInSec { get; set; }
}

public class CardReponse
{
    public string TransCode { get; set; }

    public int ResponseCode { get; set; }

    public string ResponseMessage { get; set; }

    public int TotalTopupAmount { get; set; }
}