using System;

namespace GMB.Topup.Gw.Model.Events;

public interface IEvent
{
    Guid CorrelationId { get; }
    DateTime Timestamp { get; }
}