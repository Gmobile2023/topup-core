using System;

namespace Topup.Stock.Contracts.Events;

public interface CardStockCommandDone
{
    Guid Id { get; set; }
    DateTime Timestamp { get; set; }
}