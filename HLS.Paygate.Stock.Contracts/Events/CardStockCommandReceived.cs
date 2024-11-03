using System;

namespace HLS.Paygate.Stock.Contracts.Events;

public interface CardStockCommandReceived
{
    Guid Id { get; set; }
    object StockCommand { get; set; }
    DateTime Timestamp { get; set; }
}