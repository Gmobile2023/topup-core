using System;
using Automatonymous;
using Topup.Gw.Model.Dtos;
using MassTransit;
using MassTransit.Saga;

namespace Topup.Worker.Components.StateMachines;

public class AirtimeSaleState : SagaStateMachineInstance, ISagaVersion
{
    public SaleRequestDto SaleRequest { get; set; }
    public string CurrentState { get; set; }
    public Guid? TopupRecheckToken { get; set; }
    public int RecheckTimes { get; set; }
    public int WaitTimeToCheckAgain { get; set; }
    public DateTime? SubmitDate { get; set; }
    public DateTime? Updated { get; set; }
    public int Version { get; set; }
    public Guid CorrelationId { get; set; }
}