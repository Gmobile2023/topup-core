using System;
using MassTransit;

namespace GMB.Topup.Contracts.Commands;

public interface ICommand : CorrelatedBy<Guid>
{
    DateTime TimeStamp { get; }
}