using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.CacheManager;
using Topup.Shared.Dtos;
using Topup.Shared.Utils;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using Topup.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using Serilog;
using ServiceStack;

namespace Topup.TopupGw.Components.Connectors.CG2022;

public class CG2022Connector : IGatewayConnector
{
    private readonly ICacheManager _cacheManager;
    private readonly ILogger<CG2022Connector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;


    public CG2022Connector(ITopupGatewayService topupGatewayService,
        ILogger<CG2022Connector> logger, ICacheManager cacheManager)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
        _cacheManager = cacheManager;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        _logger.Log(LogLevel.Information,
            $"CG2022Connector Topup request {topupRequestLog.ReceiverInfo} {topupRequestLog.TransRef} {topupRequestLog.TransCode} {topupRequestLog.ProviderMaxWaitingTimeout} {topupRequestLog.ProviderSetTransactionTimeout}");
        var responseMessage = new MessageResponseBase();
        try
        {
            if (!_topupGatewayService.ValidConnector(ProviderConst.CG2022, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef} - {providerInfo.ProviderCode} - CG2022Connector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var typeProvider = "";
            if (topupRequestLog.ProductCode.StartsWith("VMS"))
                typeProvider = "VMS";
            else if (topupRequestLog.ProductCode.StartsWith("VTE"))
                typeProvider = "VTE";
            else if (topupRequestLog.ProductCode.StartsWith("VNA"))
                typeProvider = "VNA";

            var request = new CG2022Request
            {
                PartnerCode = providerInfo.Username,
                TxnId = topupRequestLog.TransCode,
                Receiver = topupRequestLog.ReceiverInfo,
                Amount = topupRequestLog.TransAmount,
                ReceiverProvider = typeProvider,
                ReceiverType = "PREPAID",
                RequestType = 1,
                Timeout = topupRequestLog.ProviderSetTransactionTimeout ?? 0
            };

            request.Signature =
                $"{providerInfo.Password}{providerInfo.Username}{request.TxnId}{request.ReceiverProvider}{request.ReceiverType}{request.Receiver}{request.Amount}"
                    .EncryptMd5();
            responseMessage.TransCodeProvider = topupRequestLog.TransCode;

            try
            {
                var result = await CallApi(providerInfo, request, "CREATE");
                responseMessage.ProviderResponseCode = result?.responseStatus?.errorCode;
                responseMessage.ProviderResponseMessage = result?.responseStatus?.message;

                if (result.responseStatus.errorCode is ResponseCodeConst.Error or "501102")
                {
                    var begin = DateTime.Now;
                    Thread.Sleep(1500);
                    request.Signature = $"{providerInfo.Password}{providerInfo.Username}{request.TxnId}".EncryptMd5();
                    result = await CallApi(providerInfo, request, "CHECK");
                    var noQuery = 1;
                    while (result.responseStatus.errorCode is "03" or "16" or "501102")
                    {
                        Thread.Sleep(1000);
                        var key =
                            $"PayGate_TopupRequest:Items:{string.Join("_", providerInfo.ProviderCode, request.TxnId)}";
                        var responseCache = await _cacheManager.GetEntity<TopupRequestLogDto>(key);
                        if (responseCache != null &&
                            (responseCache.Status is TransRequestStatus.Success or TransRequestStatus.Fail))
                        {
                            result.responseStatus.errorCode = responseCache.Status == TransRequestStatus.Success
                                ? ResponseCodeConst.Error
                                : ResponseCodeConst.Success;
                            result.responseStatus.transCode = responseCache.TransCode;
                            result.responseStatus.message = "Ket qua tu cache callBack_NCC";
                            _logger.LogInformation(request.TxnId +
                                                   $"CG2022Connector Lay_ket_qua_tu cache: {result.ToJson()}");

                            break;
                        }


                        result = await CallApi(providerInfo, request, "CHECK");

                        var end = DateTime.Now;
                        noQuery++;
                        var maxWaitTimeout = topupRequestLog.ProviderMaxWaitingTimeout is > 0
                            ? topupRequestLog.ProviderMaxWaitingTimeout
                            : providerInfo.TimeoutProvider;
                        if (end.Subtract(begin).TotalSeconds > maxWaitTimeout)
                        {
                            if (result.responseStatus.errorCode is "03" or "05" or "16" or "501102")
                            {
                                var timmeQuery = await _topupGatewayService.GetTopupRequestLogAsync(
                                    topupRequestLog.TransRef,
                                    topupRequestLog.ProviderCode);
                                if (timmeQuery != null &&
                                    (timmeQuery.Status is TransRequestStatus.Success or TransRequestStatus.Fail))
                                {
                                    _logger.Log(LogLevel.Information, "Get TopupLog reponse : " + timmeQuery.ToJson());
                                    result.responseStatus.errorCode = responseCache.Status == TransRequestStatus.Success
                                        ? ResponseCodeConst.Error
                                        : ResponseCodeConst.Success;
                                    result.responseStatus.transCode = responseCache.TransCode;
                                    result.responseStatus.message = "Ket qua tu cache callBack_NCC";
                                }
                            }

                            break;
                        }
                    }

                    _logger.LogInformation(request.TxnId +
                                           $" CG2022Connector TopupReturn: {topupRequestLog.ProviderCode}-{result.ToJson()}");

                    try
                    {
                        if (result.responseStatus.errorCode == ResponseCodeConst.Error)
                        {
                            topupRequestLog.ModifiedDate = DateTime.Now;
                            topupRequestLog.TransAmount = topupRequestLog.TransAmount;
                            topupRequestLog.ResponseInfo = result.ToJson();
                            _logger.LogInformation(
                                $"CG2022Connector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                            topupRequestLog.Status = TransRequestStatus.Success;
                            responseMessage.ResponseCode = ResponseCodeConst.Success;
                            responseMessage.ResponseMessage = "Thành công";
                            responseMessage.ExtraInfo = topupRequestLog.TransAmount.ToString();
                        }
                        else if (result.responseStatus.errorCode is "03" or "05" or "16" or "501102")
                        {
                            _logger.LogInformation(
                                $"CG2022Connector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                            responseMessage.ResponseMessage =
                                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                            topupRequestLog.Status = TransRequestStatus.Timeout;
                            topupRequestLog.ModifiedDate = DateTime.Now;
                            topupRequestLog.TopupGateTimeOut = ResponseCodeConst.ResponseCode_TimeOut;
                            // var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.CG2022,
                            //     result.responseStatus.errorCode, providerInfo.ProviderCode);
                            // if (reResult != null)
                            //     responseMessage.ResponseMessage = reResult.ReponseName;
                        }
                        else
                        {
                            topupRequestLog.ModifiedDate = DateTime.Now;
                            topupRequestLog.ResponseInfo = result.ToJson();
                            _logger.LogInformation(
                                $"CG2022Connector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                            var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.CG2022,
                                result.responseStatus.errorCode, providerInfo.ProviderCode);
                            topupRequestLog.Status = TransRequestStatus.Fail;
                            responseMessage.ResponseCode =
                                reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                            responseMessage.ResponseMessage = reResult != null
                                ? reResult.ResponseName
                                : "Provider error";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation(
                            $"CG2022Connector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result} Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                        responseMessage.ResponseMessage =
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                        topupRequestLog.TopupGateTimeOut = ResponseCodeConst.ResponseCode_TimeOut;
                        topupRequestLog.Status = TransRequestStatus.Timeout;
                    }
                }
                else
                {
                    topupRequestLog.ModifiedDate = DateTime.Now;
                    topupRequestLog.ResponseInfo = result.ToJson();
                    _logger.LogInformation(
                        $"CG2022Connector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.CG2022, ResponseCodeConst.Success,
                            providerInfo.ProviderCode);
                    topupRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage =
                        reResult != null ? reResult.ResponseName : "Provider error";
                    topupRequestLog.ModifiedDate = DateTime.Now;
                }
            }
            catch (Exception parentEx)
            {
                _logger.LogInformation(
                    $"CG2022Connector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef} - ParentException: {parentEx.Message}|{parentEx.StackTrace}|{parentEx.InnerException}");
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                topupRequestLog.TopupGateTimeOut = ResponseCodeConst.ResponseCode_TimeOut;
                topupRequestLog.Status = TransRequestStatus.Timeout;
            }

            await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);


            await _cacheManager.ClearCache($"TopupRequestLog_{topupRequestLog.ProviderCode}_{request.TxnId}");
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

    public Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
    {
        throw new NotImplementedException();
    }

    public Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode)
    {
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
    {
        try
        {
            //_logger.LogInformation($"{transCodeToCheck}-CG2022Connector Check request: " + transCode);
            var responseMessage = new MessageResponseBase();

            if (providerInfo == null)
                providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null ||
                !_topupGatewayService.ValidConnector(ProviderConst.CG2022, providerInfo.ProviderCode))
            {
                _logger.LogError($"{transCode}-{providerCode}-CG2022Connector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            var request = new CG2022Request
            {
                PartnerCode = providerInfo.Username,
                TxnId = transCodeToCheck
            };

            var extraInfo = providerInfo.ExtraInfo.Split('|').Length >= 2
                ? providerInfo.ExtraInfo.Split('|')[1]
                : providerInfo.ExtraInfo.Split('|')[0];
            _logger.LogInformation($"{transCodeToCheck} CG2022Connector CheckTrans  send: " + request.ToJson());
            request.Signature = $"{providerInfo.Password}{providerInfo.Username}{request.TxnId}".EncryptMd5();
            var checkResult = await CallApi(providerInfo, request, "CHECK");
            responseMessage.ProviderResponseCode = checkResult?.responseStatus?.errorCode;
            responseMessage.ProviderResponseMessage = checkResult?.responseStatus?.message;

            if (checkResult.responseStatus.errorCode == ResponseCodeConst.Error)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
            }
            else if (extraInfo.Contains(checkResult.responseStatus.errorCode))
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Provider error";
            }
            else
            {
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                // var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("CG2022",
                //     checkResult.responseStatus.errorCode, providerCode);
                //
                // if (reResult != null)
                //     responseMessage.ResponseMessage = reResult.ReponseName;
            }

            return responseMessage;
        }
        catch (Exception e)
        {
            _logger.LogInformation(
                $"{transCodeToCheck}-CG2022Connector Check Exception: {e.Message}|{e.StackTrace}|{e.InnerException}");
            return new MessageResponseBase
            {
                ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult
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
        return await Task.FromResult(new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Nhà cung cấp không hỗ trợ thanh toán."
        });
    }

    private async Task<CG2022Reponse> CallApi(ProviderInfoDto providerInfo, CG2022Request request, string actionType)
    {
        var client = new JsonHttpClient(providerInfo.ApiUrl)
        {
            HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                BaseAddress = new Uri(providerInfo.ApiUrl)
            }
        };
        try
        {
            if (actionType == "CREATE")
            {
                _logger.LogInformation(
                    $"{request.TxnId}|TimeOutWait:{providerInfo.Timeout} CG2022Connector CREATE_Topup send: {request.ToJson()}");
                var result = await client.PostAsync<CG2022Reponse>("api/v1/cg/buy/request", request);
                _logger.LogInformation(
                    $"{request.TxnId} CG2022Connector Topup Response: {result.ToJson()}");
                return result;
            }
            else
            {
                Console.WriteLine(
                    $"{request.TxnId}|TimeOutWait:{providerInfo.Timeout} CG2022Connector Checktrans send: {request.ToJson()}");
                var result = await client.PostAsync<CG2022Reponse>("api/v1/cg/buy/request/checktrans", request);
                if (result.responseStatus.errorCode != "03" && result.responseStatus.errorCode != "05" &&
                    result.responseStatus.errorCode != "16")
                    _logger.LogInformation($"{request.TxnId} CG2022Connector Topup Response: {result.ToJson()}");
                Console.WriteLine($"{request.TxnId} CG2022Connector Topup Response: {result.ToJson()}");
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.TxnId} CG2022Connector Exception: " + ex);
            await Task.Delay(2000);
            return new CG2022Reponse
            {
                responseStatus = new responseStatus
                {
                    errorCode = "501102",
                    message = "Chưa có kết quả",
                    transCode = request.TxnId
                },
            };
        }
    }
}

public class CG2022Request
{
    public string PartnerCode { get; set; }
    public string TxnId { get; set; }
    public string ReceiverProvider { get; set; }
    public string Receiver { get; set; }
    public string ReceiverType { get; set; }
    public int Amount { get; set; }
    public int RequestType { get; set; }
    public string Signature { get; set; }
    public int Timeout { get; set; }
}

public class CG2022Reponse
{
    public responseStatus responseStatus { get; set; }
}

public class responseStatus
{
    public string transCode { get; set; }

    public string errorCode { get; set; }

    public string message { get; set; }
}

public class responseResults
{
    public int RequestAmount { get; set; }

    public int ActualAmount { get; set; }

    public PartnerRequestStatus requestStatus { get; set; }
}