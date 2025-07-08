using System;
using System.Net.Http;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.Dtos;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using Topup.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace Topup.TopupGw.Components.Connectors.SHT;

public class SHTConnector : IGatewayConnector
{
    private readonly ILogger<SHTConnector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;


    public SHTConnector(ITopupGatewayService topupGatewayService,
        ILogger<SHTConnector> logger)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        _logger.Log(LogLevel.Information, "SHTConnector Topup request: " + topupRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        if (!_topupGatewayService.ValidConnector(ProviderConst.SHT, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef} - {providerInfo.ProviderCode} - SHTConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var request = new ShtRequest
        {
            TransCode = topupRequestLog.TransCode,
            ReceiverNumber = topupRequestLog.ReceiverInfo,
            Amount = topupRequestLog.TransAmount
        };


        responseMessage.TransCodeProvider = topupRequestLog.TransCode;

        var result = await CallApi(providerInfo, request, "TOPUP");
        responseMessage.ProviderResponseCode = result?.responseStatus?.errorCode;
        responseMessage.ProviderResponseMessage = result?.responseStatus?.message;
        _logger.LogInformation(request.TransCode +
                               $" SHTConnector TopupReturn: {topupRequestLog.ProviderCode}-{result.ToJson()}");
        try
        {
            var extraInfo = providerInfo.ExtraInfo ?? string.Empty;
            if (result.responseStatus.errorCode == ResponseCodeConst.Error)
            {
                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.TransAmount = topupRequestLog.TransAmount;
                topupRequestLog.ResponseInfo = result.ToJson();
                _logger.LogInformation(
                    $"SHTConnector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                topupRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
                responseMessage.ExtraInfo = topupRequestLog.TransAmount.ToString();
            }
            else if (extraInfo.Contains(result.responseStatus.errorCode))
            {
                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.ResponseInfo = result.ToJson();
                _logger.LogInformation(
                    $"SHTConnector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("SHT",
                    result.responseStatus.errorCode, providerInfo.ProviderCode);
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode =
                    reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                responseMessage.ResponseMessage = reResult != null
                    ? reResult.ResponseName
                    : "Giao dịch lỗi phía NCC";
            }
            else
            {
                _logger.LogInformation(
                    $"SHTConnector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                topupRequestLog.Status = TransRequestStatus.Timeout;
                topupRequestLog.ModifiedDate = DateTime.Now;

                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("SHT",
                    result.responseStatus.errorCode, providerInfo.ProviderCode);
                if (reResult != null)
                    responseMessage.ResponseMessage = reResult.ResponseName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(
                $"SHTConnector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result} Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            responseMessage.ResponseMessage =
                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
            topupRequestLog.Status = TransRequestStatus.Timeout;
            //content = result.ToJson() + "|" + ex.Message;
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
            _logger.LogInformation($"{transCodeToCheck}-SHTConnector Check request: " + transCode);
            var responseMessage = new MessageResponseBase();

            if (providerInfo == null)
                providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null ||
                !_topupGatewayService.ValidConnector(ProviderConst.SHT, providerInfo.ProviderCode))
            {
                _logger.LogError($"{transCode}-{providerCode}-SHTConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            var request = new ShtRequest
            {
                TransCode = transCodeToCheck
            };

            _logger.LogInformation($"{transCodeToCheck} SHTConnector CheckTrans  send: " + request.ToJson());

            var checkResult = await CallApi(providerInfo, request, "CHECK");
            responseMessage.ProviderResponseCode = checkResult?.responseStatus?.errorCode;
            responseMessage.ProviderResponseMessage = checkResult?.responseStatus?.message;
            var extraInfo = providerInfo.ExtraInfo ?? string.Empty;
            if (checkResult.responseStatus.errorCode == ResponseCodeConst.Error)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
            }
            else if (extraInfo.Contains(checkResult.responseStatus.errorCode))
            {
                responseMessage.ResponseCode =  ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Giao dịch lỗi phía NCC";
            }
            else
            {
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("SHT",
                    checkResult.responseStatus.errorCode, providerCode);

                if (reResult != null)
                    responseMessage.ResponseMessage = reResult.ResponseName;
            }


            return responseMessage;
        }
        catch (Exception e)
        {
            _logger.LogInformation(
                $"{transCodeToCheck}-SHTConnector Check Exception: {e.Message}|{e.StackTrace}|{e.InnerException}");
            return new MessageResponseBase
            {
                ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                ResponseCode = ResponseCodeConst.ResponseCode_TimeOut
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
        _logger.Log(LogLevel.Information, "QueryBalanceAsync request: " + providerCode);
        var responseMessage = new MessageResponseBase();
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

        if (providerInfo == null)
        {
            _logger.LogInformation("providerInfo is null");
            return responseMessage;
        }

        if (!_topupGatewayService.ValidConnector(ProviderConst.SHT, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{providerCode}-{transCode}-{providerInfo.ProviderCode}-SHTConnector ProviderConnector not valid");
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
            var result = await HttpHelper.Post<ShtReponse, CheckBalanceRequest>(providerInfo.ApiUrl,
                "/api/v1/sht/control", request, timeout: TimeSpan.FromSeconds(providerInfo.Timeout));
            _logger.LogInformation($"{providerCode} sht_control Reponse: {result.ToJson()}");

            if (result != null && result.responseStatus.errorCode == ResponseCodeConst.Error)
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
        return await Task.FromResult(new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Nhà cung cấp không hỗ trợ thanh toán."
        });
    }

    private async Task<ShtReponse> CallApi(ProviderInfoDto providerInfo, ShtRequest request, string actionType)
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
            if (actionType == "TOPUP")
            {
                _logger.LogInformation(
                    $"{request.TransCode}|TimeOutWait:{providerInfo.Timeout} SHTConnector CREATE_Topup send: {request.ToJson()}");
                var result = await client.PostAsync<ShtReponse>("api/v1/sht/topup", request);
                _logger.LogInformation(
                    $"{request.TransCode} SHTConnector Topup Response: {result.ToJson()}");
                return result;
            }
            else
            {
                _logger.LogInformation(
                    $"{request.TransCode}|TimeOutWait:{providerInfo.Timeout} SHTConnector Checktrans send: {request.ToJson()}");
                var result = await client.GetAsync<ShtReponse>("api/v1/sht/topup?TransCode=" + request.TransCode);
                _logger.LogInformation(
                    $"{request.TransCode} SHTConnector Topup Response: {result.ToJson()}");

                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.TransCode} SHTConnector Topup Exception: " + ex);
            var exReponse = ex.GetResponseStatus();
            return new ShtReponse
            {
                responseStatus = new responseStatus
                {
                    errorCode = exReponse.ErrorCode,
                    message = exReponse.Message
                }
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

public class ShtRequest
{
    public string TransCode { get; set; }

    public string ReceiverNumber { get; set; }

    public int Amount { get; set; }
}

public class ShtReponse
{
    public responseStatus responseStatus { get; set; }

    public string Results { get; set; }
}

public class responseStatus
{
    public string transCode { get; set; }

    public string errorCode { get; set; }

    public string message { get; set; }
}

internal class CheckBalanceRequest
{
    public string Command { get; set; }
}