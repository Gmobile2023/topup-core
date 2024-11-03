using System;

namespace GMB.Topup.Gw.Model.Events;

public interface TopupCommandRejected
{
    Guid Id { get; }
    DateTime Timestamp { get; }

    string Reason { get; }
}

public interface TopupGameCommandRejected
{
    Guid Id { get; }
    DateTime Timestamp { get; }

    string Reason { get; }
}