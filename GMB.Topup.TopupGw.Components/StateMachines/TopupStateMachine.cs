using System;
using System.Threading.Tasks;
using GMB.Topup.Gw.Model.Events.TopupGw;
using GMB.Topup.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace GMB.Topup.TopupGw.Components.StateMachines;

public class TopupStateMachine : MassTransitStateMachine<TopupState>
{
    public TopupStateMachine(ILogger<TopupStateMachine> logger)
    {
        // Event(() => TopupSubmitted, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TopupInitialed, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TopupTimedOut, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TopupCompleted, x => x.CorrelateById(m => m.Message.CorrelationId));


        Schedule(() => TransactionCheck, x => x.TopupRecheckToken, s =>
        {
            s.Delay = TimeSpan.FromHours(1);
            s.Received = x => x.CorrelateById(m => m.Message.CorrelationId);
        });

        Request(() => SomeRequest, x => x.CorrelationId, cfg => { cfg.ServiceAddress = new Uri(""); });

        InstanceState(x => x.CurrentState);

        // Initially(
        //     When(TopupSubmitted)
        //         .Then(context =>
        //         {
        //             context.Saga.SubmitDate = context.Message.Timestamp;
        //             // context.Saga.TopupRequestLog = context.Message.SaleRequest;
        //             context.Saga.Updated = DateTime.UtcNow;
        //         })
        //         .TransitionTo(Initialed));

        During(Initialed,
            When(TopupSentToProvider)
                .TransitionTo(SentToProvider));

        During(SentToProvider,
            When(TopupTimedOut)
                .Schedule(TransactionCheck,
                    context => context.Init<TimeoutTransactionCheck>(new {context.Message.CorrelationId}),
                    context => context.Message.WaitTimeToCheckAgain)
                .TransitionTo(TimeOut),
            When(TopupCompleted)
                .TransitionTo(Complete)
                .Finalize());

        During(TimeOut,
            When(TransactionCheck.Received)
                .ThenAsync(async context =>
                {
                    logger.LogInformation("TimeoutTransactionCheck {CorrelationId}",
                        context.Saga.CorrelationId);
                    // context.Message.
                    context.Saga.RecheckTimes = context.Saga.RecheckTimes + 1;
                    // if (context.Saga.RecheckTimes <= 3)
                    // {
                    //     var gatewayConnector =
                    //         _gatewayConnectorFactory.GetServiceByKey(context.Saga.TopupRequestLog.ProviderCode);
                    //
                    //     var result = await gatewayConnector.TransactionCheckAsync(
                    //         context.Saga.TopupRequestLog.TransCode,
                    //         DateTime.Now.ToString("yyMMddHHmmssffff"),
                    //         context.Saga.TopupRequestLog.ServiceCode);
                    //
                    //     logger.LogInformation("TimeoutTransactionCheck result {result}", result);
                    // }
                    await Task.CompletedTask;
                })
            // .If(c => c.Instance.RecheckTimes <= 3, () =>
            // {
            //
            // })
        );
    }

    public Request<TopupState, TimeoutTransactionCheck, MessageResponseBase> SomeRequest { get; private set; }


    public Schedule<TopupState, TimeoutTransactionCheck> TransactionCheck { get; set; }
    public State Initialed { get; private set; }
    public State Submitted { get; private set; }
    public State SentToProvider { get; set; }
    public State TimeOut { get; private set; }
    public State Complete { get; private set; }

    public Event<TopupInitialed> TopupInitialed { get; private set; }

    // public Event<TopupSubmitted> TopupSubmitted { get; private set; }
    public Event<TopupTimedOut> TopupTimedOut { get; private set; }
    public Event<TopupCompleted> TopupCompleted { get; private set; }
    public Event<TopupSentToProvider> TopupSentToProvider { get; private set; }
}

public class TopupStateMachineDefinition :
    SagaDefinition<TopupState>
{
    public TopupStateMachineDefinition()
    {
        ConcurrentMessageLimit = 50;
    }

    protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator,
        ISagaConfigurator<TopupState> sagaConfigurator)
    {
        //endpointConfigurator.UseMessageRetry(r => r.Intervals(500, 5000, 10000));
        endpointConfigurator.UseInMemoryOutbox();
    }
}