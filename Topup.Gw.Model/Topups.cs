using System;
using System.Collections.Generic;
using Topup.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;
using Topup.Gw.Model.Dtos;

namespace Topup.Gw.Model;

[Route("/api/v1/topup", "POST")]
public class TopupRequest : IPost, IUserInfoRequest, IReturn<ResponseMessageApi<object>>
{
    public string ReceiverInfo { get; set; }
    public int Amount { get; set; }
    [Required] public string TransCode { get; set; }

    [Required] public string CategoryCode { get; set; }
    [Required] public string ServiceCode { get; set; }
    [Required] public string ProductCode { get; set; }
    public Channel Channel { get; set; }
    [Required] public string PartnerCode { get; set; }

    public string StaffAccount { get; set; }
    public SystemAccountType AccountType { get; set; }
    public AgentType AgentType { get; set; }
    public string ParentCode { get; set; }
}

// [Route("/api/v1/transaction", "POST")]
// public class TransactionRequest : IPost, IReturn<MessageResponseBase>
// {
//     [Required]
//     public string Receiver { get; set; }
//     [Required]
//     public decimal Amount { get; set; }
//     public int Timeout { get; set; }
//     [Required]
//     public string TransCode { get; set; }
//     [Required]
//     public string Signature { get; set; }
//
//     [Required]
//     public string Provider { get; set; }
//     [Required]
//     public string PartnerCode { get; set; }
//     public decimal DiscountRate { get; set; }
//     public string TopupCommand { get; set; }
// }

[Route("/api/v1/topups", "GET")]
public class TopupListGetRequest : PaggingBase, IUserInfoRequest, IGet, IReturn<MessagePagedResponseBase>
{
    public List<SaleRequestStatus> Status { get; set; }
    public SaleType SaleType { get; set; }
    public string MobileNumber { get; set; }
    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public string ProviderTransCode { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }

    public List<string> ServiceCodes { get; set; }
    public List<string> CategoryCodes { get; set; }
    public List<string> ProductCodes { get; set; }
    public string Vendor { get; set; }
    public string Filter { get; set; }
    public List<string> ProviderCode { get; set; }
    public string ReceiverType { get; set; }
    public bool IsDiscountPaid { get; set; }
    public SystemAccountType AccountType { get; set; }
    public AgentType AgentType { get; set; }
    public string ParentCode { get; set; }
    public string PartnerCode { get; set; }
    public string StaffAccount { get; set; }
    public string ProviderResponseCode { get; set; }
    public string ReceiverTypeResponse { get; set; }
    public string ParentProvider { get; set; }
}

[Route("/api/v1/topup/update", "PATCH")]
public class TopupUpdateRequest : IPatch, IReturn<MessageResponseBase>
{
    public SaleRequestStatus Status { get; set; }
    public decimal PaymentAmount { get; set; }
    public string PaymentTransCode { get; set; }
    public string Provider { get; set; }
    public string TransCode { get; set; }
    public string TransRef { get; set; }
    public string ProviderTransCode { get; set; }
}

[Route("/api/v1/topuppartner", "GET")]
public class TopupPartnerCheck : IGet, IReturn<MessageResponseBase>
{
    public string TransCode { get; set; }
    public string PartnerCode { get; set; }
    public string Signature { get; set; }
}

[Route("/api/v1/topup", "DELETE")]
[Route("/api/v1/topup/{TransCode}", "DELETE")]
public class TopupCancel : IDelete, IReturn<MessageResponseBase>
{
    public string TransCode { get; set; }
    public string Signature { get; set; }
    public string PartnerCode { get; set; }
}