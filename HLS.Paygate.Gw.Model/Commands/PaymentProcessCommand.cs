namespace HLS.Paygate.Gw.Model.Commands;

public interface PaymentProcessCommand : ICommand
{
    //TopupRequestDto TopupRequest { get; }

    string AccountCode { get; }
    string CurrencyCode { get; }
    decimal PaymentAmount { get; }
    string TransRef { get; }
    string TransNote { get; set; }
    string ServiceCode { get; set; }
    string CategoryCode { get; set; }
}