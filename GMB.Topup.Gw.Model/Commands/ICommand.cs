using System;

namespace GMB.Topup.Gw.Model.Commands;

public interface ICommand
{
    Guid CorrelationId { get; }
}