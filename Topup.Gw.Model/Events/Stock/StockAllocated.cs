using System;

namespace Topup.Gw.Model.Events.Stock;

public interface StockAllocated
{
    Guid AllocationId { get; }
    string TransCode { get; }
}