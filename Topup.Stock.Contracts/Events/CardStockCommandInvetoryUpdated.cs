using System;

namespace Topup.Stock.Contracts.Events;

public interface CardStockInvetoryUpdated
{
    Guid Id { get; set; }
    int Inventory { get; set; }
    DateTime Timestamp { get; set; }
    string Result { get; set; }
}