namespace HLS.Paygate.Gw.Model.Commands;

public interface PaymentCancelCommand : ICommand
{
    public string TransCode { get; set; }
    public string PaymentTransCode { get; set; }
    string AccountCode { get; }
    decimal RevertAmount { get; }
    string TransRef { get; }
    string TransNote { get; set; }
}

public interface PaymentPriorityCommand : ICommand
{
    public string TransactionCode { get; set; }
    string AccountCode { get; }
    decimal Amount { get; }
    string TransRef { get; }
    string TransNote { get; set; }
}