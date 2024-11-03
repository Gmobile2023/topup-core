using System;

namespace GMB.Topup.Gw.Model.Events.Stock;

public interface StockAllocated
{
    Guid AllocationId { get; }
    string TransCode { get; }
}