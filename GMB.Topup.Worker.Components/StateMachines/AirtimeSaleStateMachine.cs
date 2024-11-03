using System;
using System.Threading.Tasks;
using GMB.Topup.Gw.Model.Commands.TopupGw;
using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Gw.Model.Events.Stock;
using GMB.Topup.Gw.Model.Events.TopupGw;
using GMB.Topup.Worker.Components.StateMachines.AirtimeSaleStateMachineActivities;
using GMB.Topup.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace GMB.Topup.Worker.Components.StateMachines;

public class AirtimeSaleStateMachine : MassTransitStateMachine<AirtimeSaleState>
{
    public AirtimeSaleStateMachine(ILogger<AirtimeSaleStateMachine> logger)
    {
        Event(() => TopupSubmitted, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TopupInitialed, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TopupPaid, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TopupSentToProvider, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TopupTimedOut, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TopupCompleted, x => x.CorrelateById(m => m.Message.CorrelationId));


        Schedule(() => TransactionCheck, x => x.TopupRecheckToken, s =>
        {
            s.Delay = TimeSpan.FromHours(1);
            s.Received = x => x.CorrelateById(m => m.Message.CorrelationId);
        });

        InstanceState(x => x.CurrentState);

        Initially(
            When(TopupSubmitted)
                .Then(context =>
                {
                    logger.LogInformation("AirtimeSaleStateMachine received: " +
                                          context.Message.SaleRequest.TransCode);
                    context.Saga.SubmitDate = context.Message.Timestamp;
                    context.Saga.SaleRequest = context.Message.SaleRequest;
                    context.Saga.Updated = DateTime.UtcNow;
                })
                .TransitionTo(Initialed));

        During(Initialed,
            When(TopupPaid)
                .Then(context =>
                {
                    logger.LogInformation("AirtimeSaleStateMachine Paid: " +
                                          context.Saga.SaleRequest.TransCode);
                    context.Saga.Updated = context.Message.Timestamp;
                })
                .TransitionTo(Paid));
        During(Paid,
            When(TopupSentToProvider)
                .Then(context =>
                {
                    logger.LogInformation("AirtimeSaleStateMachine Sent to provider: " +
                                          context.Saga.SaleRequest.TransCode);
                    context.Saga.Updated = context.Message.Timestamp;
                })
                .TransitionTo(SentToProvider));

        During(SentToProvider,
            When(TopupTimedOut)
                .Then(context =>
                {
                    logger.LogInformation("AirtimeSaleStateMachine timed out: " +
                                          context.Saga.SaleRequest.TransCode);
                    context.Saga.Updated = context.Message.Timestamp;
                })
                .Schedule(TransactionCheck,
                    context => context.Init<TimeoutTransactionCheck>(new {context.Message.CorrelationId}),
                    context => context.Message.WaitTimeToCheckAgain)
                .TransitionTo(TimeOut),
            When(TopupCompleted)
                .Then(context =>
                {
                    logger.LogInformation("AirtimeSaleStateMachine completed: " +
                                          context.Saga.SaleRequest.TransCode);
                    context.Saga.Updated = context.Message.Timestamp;
                })
                .PublishAsync(context => context.Init<StockAirtimeExported>(new
                {
                    context.Message.CorrelationId,
                    StockCode = StockCodeConst.STOCK_SALE,
                    ProviderCode = context.Saga.SaleRequest.Provider,
                    context.Saga.SaleRequest.Amount,
                    TransRef = context.Saga.SaleRequest.TransCode
                }))
                .TransitionTo(Complete)
                .Finalize());

        During(TimeOut,
            When(TransactionCheck.Received, context => context.Saga.RecheckTimes <= 3)
                .Then(context =>
                {
                    logger.LogInformation("TimeoutTransactionCheck {CorrelationId}",
                        context.Saga.CorrelationId);
                    context.Saga.RecheckTimes = context.Saga.RecheckTimes + 1;
                })
                .IfElse(context => context.Saga.RecheckTimes <= 3, binder => binder.Then(context =>
                    {
                        var endpoint = context.GetSendEndpoint(new Uri(
                            $"queue:{KebabCaseEndpointNameFormatter.Instance.Message<CheckTransCommand>()}")).Result;

                        endpoint.Send<CheckTransCommand>(new
                        {
                            context.Saga.CorrelationId,
                            context.Saga.SaleRequest.TransCode,
                            ProviderCode = context.Saga.SaleRequest.Provider
                        });
                    }),
                    binder => binder.Then(context => logger.LogInformation("Finished 3 time check timeout"))
                        .TransitionTo(Complete).Finalize()),
            When(TopupCompleted)
                .Then(context =>
                    logger.LogInformation("AirtimeSaleStateMachine timeout received completed event: " +
                                          context.Saga.SaleRequest.TransCode))
                .IfAsync(c => Task.FromResult(c.Message.Result == ResponseCodeConst.Success), binder => binder
                    //.Then(context => context.Activity(x => x.OfType<AcceptOrderActivity>()))
                    .Activity(x => x.OfType<UpdateSaleRequestStatus>())
                    .PublishAsync(context => context.Init<StockAirtimeExported>(new
                    {
                        context.Message.CorrelationId,
                        StockCode = StockCodeConst.STOCK_SALE,
                        ProviderCode = context.Saga.SaleRequest.Provider,
                        context.Saga.SaleRequest.Amount,
                        TransRef = context.Saga.SaleRequest.TransCode
                    })))
                .TransitionTo(Complete)
                .Finalize()
        );
    }


    public Schedule<AirtimeSaleState, TimeoutTransactionCheck> TransactionCheck { get; set; }
    public State Initialed { get; private set; }
    public State Paid { get; private set; }
    public State SentToProvider { get; set; }
    public State TimeOut { get; private set; }
    public State Complete { get; private set; }

    public Event<TopupInitialed> TopupInitialed { get; private set; }
    public Event<TopupPaid> TopupPaid { get; private set; }
    public Event<TopupSubmitted<SaleRequestDto>> TopupSubmitted { get; private set; }
    public Event<TopupTimedOut> TopupTimedOut { get; private set; }
    public Event<TopupCompleted> TopupCompleted { get; private set; }
    public Event<TopupSentToProvider> TopupSentToProvider { get; private set; }

    private class StockAirtimeExportedEvent : StockAirtimeExported
    {
        public Guid CorrelationId { get; }
        public DateTime Timestamp { get; }
        public string StockCode { get; }
        public string ProviderCode { get; }
        public int Amount { get; }
        public string TransRef { get; }
    }
}

public class AirtimeSaleStateMachineDefinition :
    SagaDefinition<AirtimeSaleState>
{
    public AirtimeSaleStateMachineDefinition()
    {
        ConcurrentMessageLimit = 50;
    }

    protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator,
        ISagaConfigurator<AirtimeSaleState> sagaConfigurator)
    {
        //endpointConfigurator.UseMessageRetry(r => r.Intervals(500, 5000, 10000));
        endpointConfigurator.UseInMemoryOutbox();
    }
}