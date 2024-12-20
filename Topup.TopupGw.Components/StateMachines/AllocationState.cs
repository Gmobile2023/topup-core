﻿using System;
using Automatonymous;
using MassTransit.Saga;
using MongoDB.Bson.Serialization.Attributes;

namespace HLS.Paygate.TopupGw.Components.StateMachines
{
    public class AllocationState : SagaStateMachineInstance, ISagaVersion
    {
        [BsonId]
        public Guid CorrelationId { get; set; }
        public string CurrentState { get; set; }
        public Guid? HoldDurationToken { get; set; }
        public int Version { get; set; }
    }
}