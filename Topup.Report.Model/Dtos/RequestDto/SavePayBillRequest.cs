using Topup.Shared;
using ServiceStack;

namespace Topup.Report.Model.Dtos.RequestDto;

[Route("/api/v1/report/transaction/save-bill", "POST")]
public class SavePayBillRequest
{
    public int? TenantId { get; set; }
    public string Description { get; set; }
    public string AccountCode { get; set; }
    public string ProductCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductName { get; set; }
    public string LastProviderCode { get; set; }
    public string LastTransCode { get; set; }
    public string InvoiceInfo { get; set; }
    public string InvoiceCode { get; set; }
    public string ExtraInfo { get; set; }
    public Channel Channel { get; set; }
    public bool IsLastSuccess { get; set; }
    public bool IsQueryBill { get; set; }
}

[Route("/api/v1/report/transaction/save-bill/get-bill", "GET")]
public class GetSavePayBillRequest
{
    public string AccountCode { get; set; }
    public string ProductCode { get; set; }
    public string Search { get; set; }
    public PayBillCustomerStatus Status { get; set; }
}

[Route("/api/v1/report/transaction/save-bill/remove", "DELETE")]
public class RemoveSavePayBillRequest
{
    public string AccountCode { get; set; }
    public string ProductCode { get; set; }
    public string InvoiceCode { get; set; }
}

[Route("/api/v1/report/transaction/save-bill/total-waiting-bill", "GET")]
public class GetTotalWaitingBillRequest
{
    public string AccountCode { get; set; }
}