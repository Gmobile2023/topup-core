using System;
using Automatonymous;
using HLS.Paygate.Stock.Contracts.Events;

namespace HLS.Paygate.TopupGw.Components.StateMachines
{
    public class CardStockStateMachine : MassTransitStateMachine<CardStockState>
    {
        public CardStockStateMachine()
        {
            InstanceState(x => x.CurrentState, Received, Processing, Done);

            Event(() => CardStockCommandReceived, x => x.CorrelateById(c => c.Message.Id));
            Event(() => CardStockCommandDone, x => x.CorrelateById(c => c.Message.Id));
            Event(() => CardStockInventoryUpdated, x => x.CorrelateById(c => c.Message.Id));

            Initially(
                When(CardStockCommandReceived)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .Then(context => SetInitData(context.Instance, context.Data.Timestamp))
                    .TransitionTo(Received),
                When(CardStockCommandDone)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .TransitionTo(Done));

            During(Received,
                When(CardStockInventoryUpdated)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .TransitionTo(Processing),
                When(CardStockCommandDone)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .TransitionTo(Done));

            DuringAny(
                When(CardStockCommandReceived)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .Then(context => SetInitData(context.Instance, context.Data.Timestamp)));
        }

        public State Received { get; private set; }
        public State Processing { get; private set; }
        public State Done { get; private set; }

        public Event<CardStockCommandReceived> CardStockCommandReceived { get; private set; }
        public Event<CardStockCommandDone> CardStockCommandDone { get; private set; }
        public Event<CardStockInvetoryUpdated> CardStockInventoryUpdated { get; private set; }

        static void Touch(CardStockState state, DateTime timestamp, string result = "")
        {
            if (!state.CreateTimestamp.HasValue)
                state.CreateTimestamp = timestamp;

            if (!state.UpdateTimestamp.HasValue || state.UpdateTimestamp.Value < timestamp)
                state.UpdateTimestamp = timestamp;

            if (!string.IsNullOrEmpty(result))
                state.Result = result;
        }

        static void SetReceiveTimestamp(CardStockState state, DateTime timestamp)
        {
            if (!state.ReceiveTimestamp.HasValue || state.ReceiveTimestamp.Value > timestamp)
                state.ReceiveTimestamp = timestamp;
        }

        static void SetInitData(CardStockState state, /*StockCommand command,*/ DateTime timestamp)
        {
            // state.StockCode = command.StockCode;
            // state.ProductCode = command.ProductCode;
            // state.CardValue = command.CardValue;
            // state.Command = command.Command;
            // state.AccountCode = command.AccountCode;
            if (!state.CreateTimestamp.HasValue)
                state.CreateTimestamp = timestamp;

            if (!state.ReceiveTimestamp.HasValue || state.ReceiveTimestamp.Value > timestamp)
                state.ReceiveTimestamp = timestamp;
        }
    }
}