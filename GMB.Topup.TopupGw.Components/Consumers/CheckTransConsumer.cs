using System;
using System.Threading.Tasks;
using GMB.Topup.Gw.Model.Commands.TopupGw;
using GMB.Topup.Gw.Model.Events.TopupGw;
using GMB.Topup.TopupGw.Components.Connectors;
using GMB.Topup.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace GMB.Topup.TopupGw.Components.Consumers;

public class CheckTransConsumer : IConsumer<CheckTransCommand>
{
    private readonly ILogger<CheckTransConsumer> _logger;
    private IGatewayConnector _gatewayConnector;

    public CheckTransConsumer(ILogger<CheckTransConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CheckTransCommand> context)
    {
        _logger.LogInformation("CheckTrans: " + context.Message.ToJson());

        var providerCode = context.Message.ProviderCode;
        if (string.IsNullOrEmpty(providerCode))
            throw new ArgumentNullException(nameof(providerCode));

        _gatewayConnector = HostContext.Container.ResolveNamed<IGatewayConnector>(providerCode.Split('-')[0]);
        //_logger.LogInformation($"GatewayConnector {_gatewayConnector.ToJson()} CheckTrans:{providerCode}-{context.Message.TransCode}");
        var result = await _gatewayConnector.TransactionCheckAsync(providerCode, context.Message.TransCode,
            DateTime.Now.ToString("yyMMddHHmmssffff"));

        //_logger.LogInformation("CheckTrans: " + context.Message.ToJson());

        if (result.ResponseCode != ResponseCodeConst.ResponseCode_TimeOut)
            await context.Publish<TopupCompleted>(new
            {
                context.Message.CorrelationId,
                Result = result.ResponseCode,
                Message = result.ResponseMessage
            });

        await context.RespondAsync(result);
    }
}