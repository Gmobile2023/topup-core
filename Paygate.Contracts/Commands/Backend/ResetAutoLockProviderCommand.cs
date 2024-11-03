namespace Paygate.Contracts.Commands.Backend;

public interface ResetAutoLockProviderCommand : ICommand
{
    string ProviderCode { get; }
}