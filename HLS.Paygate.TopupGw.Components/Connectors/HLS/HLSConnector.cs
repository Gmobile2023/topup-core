using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.TopupGw.Components.Connectors.SHT;
using HLS.Paygate.TopupGw.Contacts.ApiRequests;
using HLS.Paygate.TopupGw.Contacts.Dtos;
using HLS.Paygate.TopupGw.Contacts.Enums;
using HLS.Paygate.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using ServiceStack;
using CheckBalanceRequest = HLS.Paygate.TopupGw.Contacts.ApiRequests.CheckBalanceRequest;

namespace HLS.Paygate.TopupGw.Components.Connectors.HLS;

public class HLSConnector : IGatewayConnector
{
    private readonly ILogger<HLSConnector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;


    public HLSConnector(ITopupGatewayService topupGatewayService,
        ILogger<HLSConnector> logger)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        _logger.LogInformation("HLSConnector topup request: " + topupRequestLog.ToJson());
        if (!_topupGatewayService.ValidConnector(ProviderConst.HLS, topupRequestLog.ProviderCode))
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };

        var responseMessage = new MessageResponseBase { TransCodeProvider = topupRequestLog.TransCode };
        topupRequestLog.Status = TransRequestStatus.Timeout;
        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
        responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
        responseMessage.ProviderResponseCode = ResponseCodeConst.ResponseCode_NT_CODE;
        responseMessage.ProviderResponseMessage =
            $"Giao dịch {topupRequestLog.CategoryCode}-{topupRequestLog.ProductCode} vào kênh HLS chưa có KQ. Vui lòng xử lý giao dịch";
        if (providerInfo == null)
        {
            Console.WriteLine("providerInfo is null");
            return responseMessage;
        }
        await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
        return responseMessage;
    }

    public Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null,
        ProviderInfoDto providerInfo = null)
    {
        _logger.LogInformation($"{transCodeToCheck} HLSConnector check request: " + transCodeToCheck + "|" + transCode);
        return Task.FromResult(new MessageResponseBase
        {
            ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
            ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
            ProviderResponseCode = ResponseCodeConst.ResponseCode_NT_CODE,
            ProviderResponseMessage = "Giao dịch không thể check KQ. Vui lòng xử lý bằng tay",
            Exception = "Hàm chưa sẵn sàng"
        });
    }

    public async Task<NewMessageReponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.LogInformation($"{payBillRequestLog.TransCode} HLSConnector Query request: " +
                               payBillRequestLog.ToJson());
        var responseMessage = new NewMessageReponseBase<InvoiceResultDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Truy vấn thông tin không thành công")
        };
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;


        if (!_topupGatewayService.ValidConnector(ProviderConst.HLS, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-HLSConnector ProviderConnector not valid");
            responseMessage.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ");
            return responseMessage;
        }

        try
        {
            using var client = new HttpClient();
            var result = await client.GetAsync(providerInfo.ApiUrl +
                                               $"/hls/api/vnpt/account/inquire?MsIsdn={payBillRequestLog.ReceiverInfo}");

            result.EnsureSuccessStatusCode();

            var resp = await result.ReadToEndAsync();

            if (string.IsNullOrEmpty(resp))
                throw new Exception("Response is empty");

            _logger.LogInformation("HLS QueryBalanceAsync result: {Id}: {Result}", payBillRequestLog.ReceiverInfo,
                resp);

            var respDto = resp.FromJson<QueryResult>();

            if (respDto.ResponseCode == "00")
            {
                var dto = new InvoiceResultDto
                {
                    Amount = decimal.Parse(respDto.Dept),
                    BillType = respDto.Telco
                };

                responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Success;
                responseMessage.ResponseStatus.Message = "Giao dịch thành công";
                responseMessage.Results = dto;
            }
            else if (respDto.ResponseCode == "18")
            {
                var dto = new InvoiceResultDto
                {
                    Amount = decimal.Parse(respDto.Dept),
                    BillType = respDto.Telco
                };

                responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Success;
                responseMessage.ResponseStatus.Message = "Giao dịch thành công";
                responseMessage.Results = dto;
            }
            else
            {
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.HLS,
                    respDto.ResponseCode, payBillRequestLog.TransCode);
                responseMessage.ResponseStatus.ErrorCode =
                    reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseStatus.Message = reResult != null ? reResult.ReponseName : "Truy vấn thất bại";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"HLS QueryBalanceAsync .Exception: " + ex.Message);
            responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Error;
            responseMessage.ResponseStatus.Message = "Truy vấn thất bại.";
        }

        return responseMessage;
    }

    public Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        return null;
    }

    public Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        return null;
    }

    public Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
    {
        return null;
    }

    public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.LogInformation("PayBillAsync:" + payBillRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        payBillRequestLog.Status = TransRequestStatus.Timeout;
        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
        responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
        responseMessage.ProviderResponseCode = ResponseCodeConst.ResponseCode_NT_CODE;
        responseMessage.ProviderResponseMessage =
            $"Giao dịch {payBillRequestLog.CategoryCode}-{payBillRequestLog.ProductCode} vào kênh HLS chưa có KQ. Vui lòng xử lý giao dịch";
        await _topupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);
        return responseMessage;
    }

    public Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
    {
        return null;
    }

    public Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode)
    {
        throw new NotImplementedException();
    }

    private class QueryResult
    {
        public string ResponseCode { get; set; }
        public string Description { get; set; }
        public string Telco { get; set; }
        public string Dept { get; set; }
    }
}