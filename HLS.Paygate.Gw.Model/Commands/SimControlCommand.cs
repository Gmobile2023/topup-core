using System;

namespace HLS.Paygate.Gw.Model.Commands
{
    public interface SimControlCommand
    {
        Guid Id { get; }
        string Command { get; }
    }
}