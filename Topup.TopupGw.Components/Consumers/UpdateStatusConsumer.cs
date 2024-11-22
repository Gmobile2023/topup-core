using System;
using System.Threading.Tasks;
using Topup.Gw.Model.Commands;
using Topup.Shared;
using Topup.Shared.CacheManager;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace Topup.TopupGw.Components.Consumers;

public class UpdateStatusConsumer : IConsumer<TopupCallBackMessage>
{
    private readonly ICacheManager _cacheManager;
    private readonly ILogger<UpdateStatusConsumer> _logger;
    private readonly ITopupGatewayService _topupGatewayService;

    public UpdateStatusConsumer(ITopupGatewayService topupGatewayService,
        ILogger<UpdateStatusConsumer> logger, ICacheManager cacheManager)
    {
        _topupGatewayService = topupGatewayService;
        _cacheManager = cacheManager;
        _logger = logger;
    }


    public async Task Consume(ConsumeContext<TopupCallBackMessage> context)
    {
        _logger.LogInformation($"{context.Message.TransCode} Received CardCallBackMessage:{context.Message.ToJson()}");
        var request = context.Message;
        var reponse = await _topupGatewayService.TopupRequestLogUpdateStatusAsync(request.TransCode,
            request.ProviderCode, request.Status == 1 ? 1 : request.Status == 2 ? 3 : 2, transAmount: request.Amount);
        bool isTopupGateTimeOut = false;
        string accountCode = string.Empty;
        isTopupGateTimeOut = request.Status == 2 && reponse.TopupGateTimeOut == ResponseCodeConst.ResponseCode_TimeOut;        
        if (isTopupGateTimeOut && request.ProviderCode == ProviderConst.CG2022)
        {
            var providerDto = await _topupGatewayService.ProviderInfoCacheGetAsync(request.ProviderCode);
            if (providerDto != null && !string.IsNullOrEmpty(providerDto.ApiUser) 
                && !string.IsNullOrEmpty(providerDto.PublicKey) 
                && providerDto.PublicKey.StartsWith("1"))            
                accountCode = providerDto.ApiUser;            
        }
        
        _logger.LogInformation($"{context.Message.TransCode} CardCallBackMessage Reponse : {request.TransCode}-{reponse?.ToJson()}");
        if (reponse != null && (request.ProviderCode == ProviderConst.GATE || isTopupGateTimeOut || request.Status == 1))
        {
            _logger.LogInformation($"Process CardCallBackMessage:{request.ToJson()}");
            await context.Publish<CallBackTransCommand>(new CallBackTransCommand
            {
                Status = request.Status == 1 ? 1 : request.Status == 2 ? 3 : 2,
                TransCode = reponse.TransRef,
                IsRefund = reponse.IsRefund,
                ProviderCode = request.ProviderCode,
                Amount = request.Amount,
                AccountCode = accountCode,
                CorrelationId = Guid.NewGuid()
            });
        }
    }
}