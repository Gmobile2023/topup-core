﻿namespace Topup.Contracts.Commands.Backend;

public interface ResetAutoLockProviderCommand : ICommand
{
    string ProviderCode { get; }
}