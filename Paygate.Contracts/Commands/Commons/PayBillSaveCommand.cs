using HLS.Paygate.Shared;

namespace Paygate.Contracts.Commands.Commons;

public interface PayBillSaveCommand : ICommand
{
    int? TenantId { get; set; }
    string Description { get; set; }
    string AccountCode { get; set; }
    string ProductCode { get; set; }
    string ProductName { get; set; }
    string CategoryCode { get; set; }
    string ServiceCode { get; set; }
    string LastProviderCode { get; set; }
    string LastTransCode { get; set; }
    string InvoiceInfo { get; set; }
    string InvoiceCode { get; set; }
    string ExtraInfo { get; set; }
    bool IsLastSuccess { get; set; }
    Channel Channel { get; set; }
}