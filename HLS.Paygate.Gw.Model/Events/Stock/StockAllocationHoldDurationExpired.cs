using System;

namespace HLS.Paygate.Gw.Model.Events.Stock;

public interface StockAllocationHoldDurationExpired
{
    Guid AllocationId { get; }
}