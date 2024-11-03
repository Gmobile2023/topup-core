using System;

namespace HLS.Paygate.Gw.Model.Events;

public interface IEvent
{
    Guid CorrelationId { get; }
    DateTime Timestamp { get; }
}