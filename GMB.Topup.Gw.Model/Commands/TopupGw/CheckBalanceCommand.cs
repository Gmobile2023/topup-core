namespace GMB.Topup.Gw.Model.Commands.TopupGw;

public interface CheckBalanceCommand : ICommand
{
    string TransCode { get; set; }
    string ProviderCode { get; set; }
}