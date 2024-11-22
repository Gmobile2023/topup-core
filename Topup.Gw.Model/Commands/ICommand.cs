using System;

namespace Topup.Gw.Model.Commands;

public interface ICommand
{
    Guid CorrelationId { get; }
}