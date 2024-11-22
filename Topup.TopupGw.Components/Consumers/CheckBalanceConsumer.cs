using System;
using System.Threading.Tasks;
using Topup.Gw.Model.Commands.TopupGw;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;
using Topup.TopupGw.Components.Connectors;

// using Topup.TopupGw.Components.Configs;


namespace Topup.TopupGw.Components.Consumers;

public class CheckBalanceConsumer : IConsumer<CheckBalanceCommand>
{
    private readonly ILogger<CheckBalanceConsumer> _logger;
    private IGatewayConnector _gatewayConnector;

    public CheckBalanceConsumer(ILogger<CheckBalanceConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CheckBalanceCommand> context)
    {
        _logger.LogInformation("CheckBalance: " + context.Message.ToJson());

        var providerCode = context.Message.ProviderCode;
        if (string.IsNullOrEmpty(providerCode))
            throw new ArgumentNullException(nameof(providerCode));

        _gatewayConnector = HostContext.Container.ResolveNamed<IGatewayConnector>(providerCode.Split('-')[0]);

        var result = await _gatewayConnector.CheckBalanceAsync(providerCode, context.Message.TransCode);
        //_logger.LogInformation($"GatewayConnector {_gatewayConnector.ToJson()} CheckBalance:{providerCode}");
        //_logger.LogInformation("CheckBalance: " + context.Message.ToJson());

        await context.RespondAsync(result);
    }
}