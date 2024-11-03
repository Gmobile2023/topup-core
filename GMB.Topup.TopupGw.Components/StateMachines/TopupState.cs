using System;
using Automatonymous;
using GMB.Topup.TopupGw.Contacts.Dtos;
using MassTransit;
using MongoDB.Bson.Serialization.Attributes;

namespace GMB.Topup.TopupGw.Components.StateMachines;

public class TopupState : SagaStateMachineInstance
{
    public string CurrentState { get; set; }

    public TopupRequestLogDto TopupRequestLog { get; set; }
    public Guid? TopupRecheckToken { get; set; }
    public int RecheckTimes { get; set; }
    public int WaitTimeToCheckAgain { get; set; }
    public DateTime? SubmitDate { get; set; }
    public DateTime? Updated { get; set; }

    [BsonId] public Guid CorrelationId { get; set; }
}