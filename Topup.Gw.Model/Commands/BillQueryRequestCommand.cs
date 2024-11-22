namespace Topup.Gw.Model.Commands;

public interface BillQueryRequestCommand : ICommand
{
    string QueryInputInfo { get; }
    string ServiceCode { get; }
    string CategoryCode { get; }
    string ProductCode { get; }
    bool IsInvoice { get; }
}