using System;

namespace HLS.Paygate.Gw.Model.Events
{
    public interface SimControlCommandRejected
    {
        Guid Id { get; }
        DateTime Timestamp { get; }
        string Reason { get; }
        string App { get; }
    }
}