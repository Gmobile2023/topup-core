using System;

namespace GMB.Topup.Gw.Model.Events.Stock;

public interface StockAllocationHoldDurationExpired
{
    Guid AllocationId { get; }
}