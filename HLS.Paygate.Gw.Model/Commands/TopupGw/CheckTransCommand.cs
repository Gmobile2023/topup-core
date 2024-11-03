namespace HLS.Paygate.Gw.Model.Commands.TopupGw;

public interface CheckTransCommand : ICommand
{
    string TransCode { get; set; }
    string ProviderCode { get; set; }
}