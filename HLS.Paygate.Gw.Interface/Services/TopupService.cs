using System;
using System.Threading;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model;
using HLS.Paygate.Gw.Model.RequestDtos;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.ConfigDtos;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.Shared.Helpers;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Paygate.Contracts.Commands.Backend;
using Paygate.Contracts.Commands.Worker;
using Paygate.Discovery.Requests.Workers;
using ServiceStack;

namespace HLS.Paygate.Gw.Interface.Services;

public partial class TopupService : Service
{
    private readonly IBusControl _bus;

    //private readonly IRequestClient<CardSaleRequestCommand> _cardSaleRequestClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TopupService> _logger;
    private readonly IPayBatchService _payBatchService;
    private readonly ISaleService _saleService;
    private readonly IRequestClient<TopupRequestCommand> _topupRequestClient;
    private readonly ISystemService _systemService;
            private readonly GrpcClientHepper _grpcClient;
    public TopupService(
        //IRequestClient<CardSaleRequestCommand> cardSaleRequestClient,
        IConfiguration configuration,
        ILogger<TopupService> logger,
        IBusControl bus, ISaleService saleService, IPayBatchService payBatchService,
        IRequestClient<TopupRequestCommand> topupRequestClient, ISystemService systemService, GrpcClientHepper grpcClient)
    {
        //_cardSaleRequestClient = cardSaleRequestClient;
        _configuration = configuration;
        _logger = logger;
        _bus = bus;
        _saleService = saleService;
        _payBatchService = payBatchService;
        _topupRequestClient = topupRequestClient;
        _systemService = systemService;
        _grpcClient = grpcClient;
    }

    public async Task<object> PostAsync(TopupRequest topupRequest)
    {
        try
        {
            _logger.LogInformation("TopupRequest {Request}", topupRequest.ToJson());
            var useQueueTopup = true;
            var useQueueTopupConfig = _configuration["RabbitMq:UseQueueTopup"];
            if (!string.IsNullOrEmpty(useQueueTopupConfig))
                useQueueTopup = bool.Parse(_configuration["RabbitMq:UseQueueTopup"]);

            var returnMessage = new NewMessageReponseBase<object>();
            if (useQueueTopup)
            {
                var rs = await _topupRequestClient.GetResponse<NewMessageReponseBase<WorkerResult>>(new
                {
                    topupRequest.Amount,
                    topupRequest.Channel,
                    topupRequest.CategoryCode,
                    topupRequest.ProductCode,
                    topupRequest.PartnerCode,
                    topupRequest.ReceiverInfo,
                    RequestIp = Request.UserHostAddress,
                    topupRequest.ServiceCode,
                    topupRequest.StaffAccount,
                    topupRequest.AgentType,
                    topupRequest.AccountType,
                    RequestDate = DateTime.Now,
                    topupRequest.TransCode,
                    topupRequest.ParentCode
                }, CancellationToken.None, RequestTimeout.After(m: 10));
                var mess = rs.Message;
                _logger.LogInformation($"{topupRequest.TransCode}-Topup web GetResponse:{mess.ToJson()}");
                returnMessage = new NewMessageReponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(mess.ResponseStatus.ErrorCode, mess.ResponseStatus.Message)
                    {
                        TransCode = topupRequest.TransCode
                    }
                };
            }
            else
            {
                var request = topupRequest.ConvertTo<WorkerTopupRequest>();
                request.RequestDate = DateTime.Now;
                request.RequestIp = Request.UserHostAddress;
                var getApi = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(request);
                returnMessage.ResponseStatus = getApi.ResponseStatus;
                returnMessage.ResponseStatus.TransCode = topupRequest.TransCode;
            }

            _logger.LogInformation($"{topupRequest.TransCode}-TopupRequest return:{returnMessage.ToJson()}");
            return returnMessage;
        }
        catch (Exception e)
        {
            _logger.LogError($"{topupRequest.TransCode}-TopupRequest error:{e}");
            return new NewMessageReponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                    "Giao dịch chưa có kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
            };
        }
    }

    public async Task<object> GetAsync(CheckTransRequest check)
    {
        _logger.LogInformation("CheckTransRequest: {Request}", check.ToJson());
        var rs = await _saleService.SaleRequestCheckAsync(check.TransCodeToCheck, check.PartnerCode);
        _logger.LogInformation("CheckTransRequestReturn: {Response}", rs.ToJson());
        return new NewMessageReponseBase<string>
        {
            Results = rs.ExtraInfo,
            ResponseStatus = new ResponseStatusApi(rs.ResponseCode, rs.ResponseMessage)
        };
    }

    public async Task<object> PostAsync(CallBackCorrectTransRequest request)
    {
        _logger.LogInformation("ProviderCallBackCorrectTransRequest: {Request}", request.ToJson());
        await _bus.Publish<CallBackCorrectTransCommand>(new
        {
            request.TransCode,
            request.ResponseCode,
            request.ResponseMessage,
            CorrelationId = Guid.NewGuid(),
            TimeStamp = DateTime.Now
        });
        return new NewMessageReponseBase<object>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Tiếp nhận thành công")
        };
    }

    public async Task<object> GetAsync(PingRouteRequest request)
    {
        return await Task.FromResult("OK");
    }
}