using System;
using System.Threading.Tasks;
using Topup.Gw.Model.Events.TopupGw;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;
using Topup.TopupGw.Components.Connectors;

namespace Topup.TopupGw.Components.StateMachines.TopupStateMachineActivities;

public class CheckTransActivity : IStateMachineActivity<TopupState, TimeoutTransactionCheck>
{
    private readonly ILogger<CheckTransActivity> _logger;

    public CheckTransActivity(ILogger<CheckTransActivity> logger)
    {
        _logger = logger;
    }

    public void Probe(ProbeContext context)
    {
        context.CreateScope("check-trans");
    }

    public void Accept(StateMachineVisitor visitor)
    {
        visitor.Visit(this);
    }

    public async Task Execute(BehaviorContext<TopupState, TimeoutTransactionCheck> context, IBehavior<TopupState, TimeoutTransactionCheck> next)
    {
        var gatewayConnector =
            HostContext.Container.ResolveNamed<IGatewayConnector>(
                context.Saga.TopupRequestLog.ProviderCode.Split('-')[0]);
        //_logger.LogInformation($"GatewayConnector {gatewayConnector.ToJson()} CheckTrans:{context.Saga.TopupRequestLog.TransRef}");
        var result = await gatewayConnector.TransactionCheckAsync(context.Saga.TopupRequestLog.ProviderCode,
            context.Saga.TopupRequestLog.TransCode,
            DateTime.Now.ToString("yyMMddHHmmssffff"));

        //_logger.LogInformation("TimeoutTransactionCheck result {result}", result);
    }

    public Task Faulted<TException>(BehaviorExceptionContext<TopupState, TimeoutTransactionCheck, TException> context, IBehavior<TopupState, TimeoutTransactionCheck> next) where TException : Exception
    {
        return next.Faulted(context);
    }
}