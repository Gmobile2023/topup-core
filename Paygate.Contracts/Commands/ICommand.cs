using System;
using MassTransit;

namespace Paygate.Contracts.Commands;

public interface ICommand : CorrelatedBy<Guid>
{
    DateTime TimeStamp { get; }
}