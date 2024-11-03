using System;
using Automatonymous;
using MassTransit;
using MassTransit.Saga;

namespace HLS.Paygate.TopupGw.Components.StateMachines
{
    public class CardStockState : CorrelatedBy<Guid>, SagaStateMachineInstance, ISagaVersion
    {
        public Guid CorrelationId { get; set; }
        public string StockCode { get; set; }
        public int Inventory { get; set; }
        public string ProductCode { get; set; }
        public decimal CardValue { get; set; }
        public int CurrentState { get; set; }
        public DateTime? ReceiveTimestamp { get; set; }
        public DateTime? CreateTimestamp { get; set; }
        public DateTime? UpdateTimestamp { get; set; }
        public int Version { get; set; }
        public string Command { get; set; }
        public string AccountCode { get; set; }
        public string Result { get; set; }
    }
}