using GMB.Topup.TopupGw.Domains.Entities;
using System.Runtime.Serialization;
using System.Security.Policy;

namespace GMB.Topup.TopupGw.Components.Connectors.Viettel2;

public class DataObject
{
    [DataMember(Name = "account_type")] public string AccountType { get; set; }
    [DataMember(Name = "acc_no")] public string AccNo { get; set; }
    [DataMember(Name = "amount")] public decimal? Amount { get; set; }   
    [DataMember(Name = "bank_code")] public string BankCode { get; set; }
    [DataMember(Name = "billing_code")] public string BillingCode { get; set; }   
    [DataMember(Name = "channel_info")] public string ChannelInfo { get; set; }
    [DataMember(Name = "order_id")] public string OrderId { get; set; }

    [DataMember(Name = "original_order_id")]
    public string OriginalOrderId { get; set; }

    [DataMember(Name = "password")] public string Password { get; set; }

    [DataMember(Name = "payer_msisdn")] public string PayerMsisdn { get; set; }
    [DataMember(Name = "quantity")] public int? Quantity { get; set; }
    [DataMember(Name = "service_code")] public string ServiceCode { get; set; }

    [DataMember(Name = "trans_date")] public string TransDate { get; set; }

    [DataMember(Name = "username")] public string Username { get; set; }

    [DataMember(Name = "trans_id")] public string TransId { get; set; }

    [DataMember(Name = "error_msg")] public string ErrorMsg { get; set; }

    [DataMember(Name = "error_code")] public string ErrorCode { get; set; }

    [DataMember(Name = "balance")] public decimal? Balance { get; set; }

    [DataMember(Name = "billing_detail")] public string BillDetail { get; set; }

    [DataMember(Name = "reference_msg")] public string ReferenceMsg { get; set; }

    [DataMember(Name = "reference_code")] public string ReferenceCode { get; set; }

    [DataMember(Name = "reference_msg")] public string ReferenceMessage { get; set; }

    [DataMember(Name = "tpp_type")] public string TppType { get; set; }

    [DataMember(Name = "original_trans_id")]
    public string Original_TransId { get; set; }
}

public class DataCardObject
{
    [DataMember(Name = "amount", Order = 1)] public decimal? Amount { get; set; }
    [DataMember(Name = "order_id", Order = 2)] public string OrderId { get; set; } 
    [DataMember(Name = "password", Order = 3)] public string Password { get; set; }
    [DataMember(Name = "payer_msisdn", Order = 4)] public string PayerMsisdn { get; set; }
    [DataMember(Name = "quantity", Order = 5)] public int? Quantity { get; set; }
    [DataMember(Name = "service_code", Order = 6)] public string ServiceCode { get; set; }
    [DataMember(Name = "username", Order = 7)] public string Username { get; set; }         
}