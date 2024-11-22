using System;

namespace HLS.Paygate.Gw.Model.Events
{
    public interface SimControlCommandResult
    {
        Guid Id { get; }
        DateTime TimeStamp { get; }
        string Result { get; }
        string App { get; }
    }
}