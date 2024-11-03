using System;
using System.Linq;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.ConfigDtos;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.TopupGw.Contacts.Dtos;
using HLS.Paygate.TopupGw.Contacts.Enums;
using HLS.Paygate.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using Paygate.Discovery.Requests.Stocks;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Components.Connectors.NhatTran;

public class NhatTranStockConnector : IGatewayConnector
{
    //private readonly IServiceGateway _gateway; gunner
    private readonly ILogger<NhatTranStockConnector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;
    private readonly GrpcClientHepper _grpcClient;

    public NhatTranStockConnector(ITopupGatewayService topupGatewayService,
        ILogger<NhatTranStockConnector> logger
        ,
        GrpcClientHepper grpcClient)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
        _grpcClient = grpcClient;
        //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        return await Task.FromResult(new MessageResponseBase());
    }

    public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
    {
        return await Task.FromResult(new MessageResponseBase());
    }

    public async Task<NewMessageReponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
        return await Task.FromResult(new NewMessageReponseBase<InvoiceResultDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Nhà cung cấp không hỗ trợ truy vấn")
        });
    }

    public async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        _logger.Log(LogLevel.Information,
            $"{cardRequestLog.TransCode} NhatTranStockConnecto request: " + cardRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();

        try
        {
            var checkStock = await _grpcClient.GetClientCluster(GrpcServiceName.Stock).SendAsync(
                new StockCardCheckInventoryRequest
                {
                    StockCode = StockCodeConst.STOCK_SALE,
                    ProductCode = cardRequestLog.ProductCode
                });
            _logger.LogInformation(
                $"NhatTranStockConnector checkStock return {cardRequestLog.TransCode}-{cardRequestLog.TransRef} {checkStock.ResponseStatus.ToJson()}");
            if (checkStock.ResponseStatus.ErrorCode != ResponseCodeConst.Success ||
                checkStock.Results < cardRequestLog.Quantity)
            {
                _logger.LogInformation(
                    $"NhatTranStockConnector {cardRequestLog.TransCode}-{cardRequestLog.TransRef}-Cardsale fail {cardRequestLog.TransCode} - Kho the khong du");

                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Kho thẻ không đủ";
                cardRequestLog.Status = TransRequestStatus.Fail;
            }
            else
            {
                var response = await _grpcClient.GetClientCluster(GrpcServiceName.Stock).SendAsync(
                    new StockCardExportSaleRequest
                    {
                        StockCode = StockCodeConst.STOCK_SALE,
                        ProductCode = cardRequestLog.ProductCode,
                        Amount = cardRequestLog.Quantity,
                        TransCode = cardRequestLog.TransCode
                    });

                _logger.LogInformation(
                    $"NhatTranStockConnector {cardRequestLog.TransCode}-{cardRequestLog.TransRef} - GetCard return {response.ResponseStatus.ToJson()}");
                //var jsonReponse = response.Results.Select(c => new { c.Serial, c.CardValue }).ToJson();
                //_logger.LogInformation($"NhatTranStockConnector {cardRequestLog.TransCode}-{cardRequestLog.TransRef} - GetCard GetCard: {jsonReponse}");
                if (response.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    cardRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.Payload = response.Results;
                }
                else
                {
                    responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
                    cardRequestLog.Status = TransRequestStatus.Fail;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(
                $"NhatTranStockConnector Error: {cardRequestLog.ProviderCode}-{cardRequestLog.TransCode}-{cardRequestLog.TransRef} Exception: {ex}");
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
        _logger.Log(LogLevel.Information, "QueryBalanceAsync request: " + providerCode);
        var responseMessage = new MessageResponseBase();
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

        if (providerInfo == null)
        {
            _logger.LogInformation($"providerCode= {providerCode}|providerInfo is null");
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

        // var client = new JsonServiceClient(providerInfo.ApiUrl)
        // { Timeout = TimeSpan.FromSeconds(providerInfo.Timeout) };

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
            responseMessage.Exception = ex.Message;
        }

        return responseMessage;
    }

    public async Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
    {
        return await Task.FromResult(new MessageResponseBase());
    }

    public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        return await Task.FromResult(new MessageResponseBase());
    }

    public Task<ResponseMessageApi<object>> GetProductInfo(Contacts.ApiRequests.GetProviderProductInfo info)
    {
        throw new NotImplementedException();
    }

    public Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode)
    {
        throw new NotImplementedException();
    }
}