using System;

namespace HLS.Paygate.Gw.Model.Events.Stock;

public interface StockAllocationCreated
{
    Guid AllocationId { get; }
    TimeSpan HoldDuration { get; }
}