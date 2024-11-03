using System;

namespace HLS.Paygate.Gw.Model.Commands;

public interface ICommand
{
    Guid CorrelationId { get; }
}