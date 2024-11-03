using System;

namespace HLS.Paygate.Gw.Model.Events.TopupGw;

public interface TopupTimedOut : IEvent
{
    TimeSpan WaitTimeToCheckAgain { get; set; }
}