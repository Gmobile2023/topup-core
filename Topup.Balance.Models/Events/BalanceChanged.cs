using Topup.Shared;
using Topup.Shared.Messages.Events;
using Topup.Balance.Models.Dtos;

namespace Topup.Balance.Models.Events;

public interface BalanceChanged : IEvent
{
    AccountBalanceDto AccountBalance { get; }
}