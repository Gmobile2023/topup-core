using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace GMB.Topup.Gw.Model;

[Route("/api/v1/pay_bill", "POST")]
public class PayBill : IPost, IUserInfoRequest, IReturn<ResponseMessageApi<object>>
{
    public string ReceiverInfo { get; set; }
    public decimal Amount { get; set; }
    [Required] public string TransCode { get; set; }

    [Required] public string CategoryCode { get; set; }
    [Required] public string ServiceCode { get; set; }
    [Required] public string ProductCode { get; set; }
    [Required] public bool IsInvoice => true;
    public bool IsSaveBill { get; set; }
    public string ExtraInfo { get; set; }
    public Channel Channel { get; set; }
    [Required] public string PartnerCode { get; set; }

    public string StaffAccount { get; set; }
    public SystemAccountType AccountType { get; set; }
    public AgentType AgentType { get; set; }
    public string ParentCode { get; set; }
}

[Route("/api/v1/bill_query", "GET")]
public class BillQuery : IGet, IReturn<ResponseMessageApi<object>>
{
    public string ReceiverInfo { get; set; }
    [Required] public string CategoryCode { get; set; }
    [Required] public string ServiceCode { get; set; }
    [Required] public string ProductCode { get; set; }
    public string TransCode { get; set; }
    public string PartnerCode { get; set; }
    [Required] public bool IsInvoice => true;
    public Channel Channel { get; set; }
}