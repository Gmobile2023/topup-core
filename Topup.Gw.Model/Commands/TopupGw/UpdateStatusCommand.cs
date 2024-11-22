namespace Topup.Gw.Model.Commands.TopupGw;

public interface UpdateStatusCommand : ICommand
{
    decimal Amount { get; }
    string ProviderCode { get; }
    string TransCode { get; }
    int Status { get; set; }
}