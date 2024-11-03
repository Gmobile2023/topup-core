using System;

namespace GMB.Topup.Shared.Messages.Events;

public interface IEvent
{
    Guid EventId { get; set; }
    DateTime Timestamp { get; set; }
}