using HLS.Paygate.Balance.Models.Dtos;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Messages.Events;

namespace HLS.Paygate.Balance.Models.Events;

public interface BalanceChanged : IEvent
{
    AccountBalanceDto AccountBalance { get; }
}