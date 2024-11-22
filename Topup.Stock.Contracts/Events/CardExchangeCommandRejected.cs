using System;

namespace Topup.Stock.Contracts.Events;

public interface CardExchangeCommandRejected1
{
    Guid Id { get; }
    DateTime Timestamp { get; }

    string Reason { get; }
}