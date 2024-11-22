namespace Topup.Contracts.Commands.Backend;

public interface LockProviderCommand : ICommand
{
    string ProviderCode { get; }
    int TimeClose { get; }
}