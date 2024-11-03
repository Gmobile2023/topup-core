using System;

namespace HLS.Paygate.Shared.Contracts.Events;
public interface IEvent
{
    Guid EventId { get; set; }
    DateTime Timestamp { get; set; }
    Guid CorrelationId { get; set; }
}