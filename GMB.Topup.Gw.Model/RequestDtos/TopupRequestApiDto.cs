using GMB.Topup.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace GMB.Topup.Gw.Model.RequestDtos;

[Route("/api/v1/transactions/topup", "POST")]
public class TopupPartnerRequest : PartnerRequestBase, IPost
{
    public string ReceiverInfo { get; set; }
    public int Amount { get; set; }
    [Required] public string TransCode { get; set; }
    [Required] public string PartnerCode { get; set; }
    [Required] public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
    public Channel Channel { get; set; }
    public bool IsCheckReceiverType { get; set; }
    public bool IsNoneDiscount { get; set; }
    public string DefaultReceiverType { get; set; }
    public bool IsCheckAllowTopupReceiverType { get; set; }
}

[Route("/api/v1/transactions/pincode", "POST")]
public class PinCodePartnerRequest : PartnerRequestBase, IPost
{
    [Required] public string TransCode { get; set; }
    [Required] public string PartnerCode { get; set; }
    [Required] public string CategoryCode { get; set; }
    public Channel Channel { get; set; }
    [Required] public int Quantity { get; set; }
    [Required] public int CardValue { get; set; }
    public string Email { get; set; }
    public string ServiceCode { get; set; }
    public string ProductCode { get; set; }
    public string ClientKey { get; set; }
}

[Route("/api/v1/transactions/pay-bill", "POST")]
public class PayBillPartnerRequest : PartnerRequestBase, IPost
{
    [Required] public string ReceiverInfo { get; set; }
    [Required] public decimal Amount { get; set; }
    [Required] public string TransCode { get; set; }
    [Required] public string PartnerCode { get; set; }
    [Required] public string CategoryCode { get; set; }
    [Required] public string ProductCode { get; set; }
    public string ExtraInfo { get; set; }
    [Required] public bool IsInvoice => true;
}

[Route("/api/v1/transactions/bill-query", "GET")]
public class BillQueryPartnerRequest : PartnerRequestBase, IGet
{
    [Required] public string ReceiverInfo { get; set; }
    [Required] public string PartnerCode { get; set; }
    [Required] public string CategoryCode { get; set; }
    [Required] public string ProductCode { get; set; }
    public string TransCode { get; set; }
    [Required] public bool IsInvoice => true;
}

[Route("/api/v1/transactions/checktrans", "GET")]
public class CheckTransRequest : PartnerRequestBase, IGet, IReturn<MessageResponseBase>
{
    public string TransCode { get; set; }
    public string TransCodeToCheck { get; set; }
    public string PartnerCode { get; set; }
}

[Route("/api/v2/transactions/checktrans", "GET")]
public class CheckTransAuthenV2Request : PartnerRequestBase, IGet, IReturn<MessageResponseBase>
{
    public string TransCode { get; set; }
    public string TransCodeToCheck { get; set; }
    public string PartnerCode { get; set; }
    public string ClientKey { get; set; }
}

[Route("/api/v1/transactions/checktrans_v2", "GET")]
public class CheckTransAuthenNewRequest : PartnerRequestBase, IGet, IReturn<MessageResponseBase>
{
    public string TransCode { get; set; }
    public string TransCodeToCheck { get; set; }
    public string PartnerCode { get; set; }
    public string ClientKey { get; set; }
}

[Route("/api/v1/transactions/check", "GET")]
public class CheckTransAuthenRequest : PartnerRequestBase, IGet, IReturn<MessageResponseBase>
{
    public string TransCode { get; set; }
    public string TransCodeToCheck { get; set; }
    public string PartnerCode { get; set; }
    public string ClientKey { get; set; }
}

[Route("/api/v1/transactions/callback-correct-trans", "POST")]
public class CallBackCorrectTransRequest
{
    public string TransCode { get; set; }
    public string ResponseCode { get; set; }

    public string ResponseMessage { get; set; }

    public string Signature { get; set; }
}