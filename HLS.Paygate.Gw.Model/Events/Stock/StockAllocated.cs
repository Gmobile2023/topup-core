using System;

namespace HLS.Paygate.Gw.Model.Events.Stock;

public interface StockAllocated
{
    Guid AllocationId { get; }
    string TransCode { get; }
}