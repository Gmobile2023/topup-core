using System;

namespace HLS.Paygate.Stock.Contracts.Events;

public interface CardExchangeCommandRejected1
{
    Guid Id { get; }
    DateTime Timestamp { get; }

    string Reason { get; }
}