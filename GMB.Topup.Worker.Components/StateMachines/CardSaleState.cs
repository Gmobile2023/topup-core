using System;
using Automatonymous;
using GMB.Topup.Gw.Domain.Entities;
using MassTransit;
using MassTransit.Saga;
using MongoDB.Bson.Serialization.Attributes;

namespace GMB.Topup.Worker.Components.StateMachines;

public class CardSaleState : SagaStateMachineInstance, ISagaVersion
{
    public string CurrentState { get; set; }
    public SaleRequest SaleRequest { get; set; }
    public int Version { get; set; }

    [BsonId] public Guid CorrelationId { get; set; }
}