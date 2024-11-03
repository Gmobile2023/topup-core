using System;

namespace HLS.Paygate.Shared.Messages.Events;

public interface IEvent
{
    Guid EventId { get; set; }
    DateTime Timestamp { get; set; }
}