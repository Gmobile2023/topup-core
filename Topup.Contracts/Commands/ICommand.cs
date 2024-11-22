using System;
using MassTransit;

namespace Topup.Contracts.Commands;

public interface ICommand : CorrelatedBy<Guid>
{
    DateTime TimeStamp { get; }
}