using System.Runtime.Serialization;
using Topup.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Topup.Gw.Model.RequestDtos;

[Route("/api/v1/partner/payment/topup", "POST")]
public class TopupPartnerRequest : PartnerRequestBase, IPost
{
    [DataMember(Name = "phone")] public string PhoneNumber { get; set; }
    public int Amount { get; set; }
    public string RequestCode { get; set; }
    [DataMember(Name = "partner")] public string PartnerCode { get; set; }
    [DataMember(Name = "provider")] public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
}

[DataContract]
[Route("/api/v1/partner/payment/pincode", "POST")]
public class PinCodePartnerRequest : PartnerRequestBase, IPost
{
    [Required] public string TransCode { get; set; }

    [DataMember(Name = "partner")]
    public string PartnerCode { get; set; }

    [DataMember(Name = "provider")]
    public string CategoryCode { get; set; }

    public Channel Channel { get; set; }
    [Required] public int Quantity { get; set; }
    [Required] public int CardValue { get; set; }
    public string Email { get; set; }
    public string ServiceCode { get; set; }
    public string ProductCode { get; set; }
    public string ClientKey { get; set; }
}

[Route("/api/v1/parner/payment/paybill", "POST")]
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

[Route("/api/v1/partner/payment/query", "GET")]
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

[Route("/api/v1/partner/payment/status", "GET")]
public class PartnerCheckTransRequest : PartnerRequestBase, IGet, IReturn<MessageResponseBase>
{
    public string RequestCode { get; set; }
    [DataMember(Name = "partner")] public string PartnerCode { get; set; }
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