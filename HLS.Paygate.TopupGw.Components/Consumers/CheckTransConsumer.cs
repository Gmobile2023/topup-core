using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Commands.TopupGw;
using HLS.Paygate.Gw.Model.Events.TopupGw;
using HLS.Paygate.Shared;
using HLS.Paygate.TopupGw.Components.Connectors;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Components.Consumers;

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