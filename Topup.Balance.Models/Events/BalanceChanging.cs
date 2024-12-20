﻿using Topup.Shared.Messages.Events;

namespace Topup.Balance.Models.Events;

public interface BalanceChanging : IEvent
{
}

public interface BalanceDepositMessage : IEvent
{
    string AccountCode { get; set; }
    string CurrencyCode { get; set; }
    decimal Amount { get; set; }
    string TransRef { get; set; }
    string Description { get; set; }
    string TransNote { get; set; }
}