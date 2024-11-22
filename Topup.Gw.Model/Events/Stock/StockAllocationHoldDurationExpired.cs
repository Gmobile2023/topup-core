using System;

namespace Topup.Gw.Model.Events.Stock;

public interface StockAllocationHoldDurationExpired
{
    Guid AllocationId { get; }
}