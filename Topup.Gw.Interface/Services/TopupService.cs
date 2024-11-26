using System;
using System.Threading;
using System.Threading.Tasks;
using Topup.Gw.Domain.Services;
using Topup.Gw.Model;
using Topup.Gw.Model.RequestDtos;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Topup.Contracts.Commands.Backend;
using Topup.Discovery.Requests.Workers;
using Topup.Shared;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Helpers;
using ServiceStack;

namespace Topup.Gw.Interface.Services;

public partial class TopupService : Service
{
    private readonly IBusControl _bus;

    private readonly ILogger<TopupService> _logger;
    private readonly IPayBatchService _payBatchService;
    private readonly ISaleService _saleService;
    private readonly GrpcClientHepper _grpcClient;

    public TopupService(
        ILogger<TopupService> logger,
        IBusControl bus, ISaleService saleService, IPayBatchService payBatchService,
        GrpcClientHepper grpcClient)
    {
        _logger = logger;
        _bus = bus;
        _saleService = saleService;
        _payBatchService = payBatchService;
        _grpcClient = grpcClient;
    }

    public async Task<object> PostAsync(TopupRequest topupRequest)
    {
        try
        {
            _logger.LogInformation("TopupRequest {Request}", topupRequest.ToJson());
            var returnMessage = new NewMessageResponseBase<object>();
            var request = topupRequest.ConvertTo<WorkerTopupRequest>();
            request.RequestDate = DateTime.Now;
            request.RequestIp = Request.UserHostAddress;
            var getApi = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(request);
            returnMessage.ResponseStatus = getApi.ResponseStatus;
            returnMessage.ResponseStatus.TransCode = topupRequest.TransCode;

            _logger.LogInformation($"{topupRequest.TransCode}-TopupRequest return:{returnMessage.ToJson()}");
            return returnMessage;
        }
        catch (Exception e)
        {
            _logger.LogError($"{topupRequest.TransCode}-TopupRequest error:{e}");
            return new NewMessageResponseBase<object>
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
        return new NewMessageResponseBase<string>
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
        return new NewMessageResponseBase<object>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Tiếp nhận thành công")
        };
    }

    public async Task<object> GetAsync(PingRouteRequest request)
    {
        return await Task.FromResult("OK");
    }
}