using System;

namespace Topup.Stock.Contracts.Events;

public interface CardExchangeCommandSubmitted1
{
    Guid Id { get; }
    DateTime Timestamp { get; }
    object Payload { get; }
}