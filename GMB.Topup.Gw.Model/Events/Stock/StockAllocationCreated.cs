using System;

namespace GMB.Topup.Gw.Model.Events.Stock;

public interface StockAllocationCreated
{
    Guid AllocationId { get; }
    TimeSpan HoldDuration { get; }
}