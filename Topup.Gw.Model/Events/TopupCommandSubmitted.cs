using System;

namespace Topup.Gw.Model.Events;

public interface TopupCommandSubmitted
{
    Guid Id { get; }
    DateTime TimeStamp { get; }
    string TransCode { get; }
}

public interface TopupGameCommandSubmitted
{
    Guid Id { get; }
    DateTime TimeStamp { get; }
    string TransCode { get; }
}