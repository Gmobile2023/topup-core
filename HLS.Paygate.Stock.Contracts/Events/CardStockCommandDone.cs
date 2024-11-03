using System;

namespace HLS.Paygate.Stock.Contracts.Events;

public interface CardStockCommandDone
{
    Guid Id { get; set; }
    DateTime Timestamp { get; set; }
}