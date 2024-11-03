﻿using System;
using Automatonymous;
using HLS.Paygate.Gw.Model.Commands.Stock;
using HLS.Paygate.Gw.Model.Events.Stock;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace HLS.Paygate.Stock.Components.StateMachines;

public class AllocationStateMachine :
    MassTransitStateMachine<AllocationState>
{
    public AllocationStateMachine(ILogger<AllocationStateMachine> logger)
    {
        Event(() => AllocationCreated, x => x.CorrelateById(m => m.Message.AllocationId));
        Event(() => ReleaseRequested, x => x.CorrelateById(m => m.Message.AllocationId));

        Schedule(() => HoldExpiration, x => x.HoldDurationToken, s =>
        {
            s.Delay = TimeSpan.FromHours(1);

            s.Received = x => x.CorrelateById(m => m.Message.AllocationId);
        });

        InstanceState(x => x.CurrentState);

        Initially(
            When(AllocationCreated)
                .Schedule(HoldExpiration,
                    context => context.Init<StockAllocationHoldDurationExpired>(new {context.Message.AllocationId}),
                    context => context.Message.HoldDuration)
                .TransitionTo(Allocated),
            When(ReleaseRequested)
                .TransitionTo(Released)
        );

        During(Allocated,
            When(AllocationCreated)
                .Schedule(HoldExpiration,
                    context => context.Init<StockAllocationHoldDurationExpired>(new {context.Message.AllocationId}),
                    context => context.Message.HoldDuration)
        );

        During(Released,
            When(AllocationCreated)
                .Then(context => logger.LogInformation("Allocation already released: {AllocationId}",
                    context.Saga.CorrelationId))
                .Finalize()
        );

        During(Allocated,
            When(HoldExpiration.Received)
                .Then(context =>
                    logger.LogInformation("Allocation expired {AllocationId}", context.Saga.CorrelationId))
                .Finalize(),
            When(ReleaseRequested)
                .Unschedule(HoldExpiration)
                .Then(context =>
                    logger.LogInformation("Allocation Release Granted: {AllocationId}", context.Saga.CorrelationId))
                .Finalize()
        );

        SetCompletedWhenFinalized();
    }

    public Schedule<AllocationState, StockAllocationHoldDurationExpired> HoldExpiration { get; set; }

    public State Allocated { get; set; }
    public State Released { get; set; }

    public Event<StockAllocationCreated> AllocationCreated { get; set; }
    public Event<StockUnAllocateCommand> ReleaseRequested { get; set; }
}