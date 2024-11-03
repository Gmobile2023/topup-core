using System;

namespace GMB.Topup.Gw.Model.Events.TopupGw;

public interface TopupTimedOut : IEvent
{
    TimeSpan WaitTimeToCheckAgain { get; set; }
}