using GMB.Topup.Balance.Models.Dtos;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Messages.Events;

namespace GMB.Topup.Balance.Models.Events;

public interface BalanceChanged : IEvent
{
    AccountBalanceDto AccountBalance { get; }
}