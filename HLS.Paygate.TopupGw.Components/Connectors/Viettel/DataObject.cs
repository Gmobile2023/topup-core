using System.Runtime.Serialization;

namespace HLS.Paygate.TopupGw.Components.Connectors.Viettel;

public class DataObject
{
    [DataMember(Name = "order_id")] public string OrderId { get; set; }

    [DataMember(Name = "service_code")] public string ServiceCode { get; set; }

    [DataMember(Name = "username")] public string Username { get; set; }

    [DataMember(Name = "password")] public string Password { get; set; }

    [DataMember(Name = "trans_date")] public string TransDate { get; set; }

    [DataMember(Name = "billing_code")] public string BillingCode { get; set; }

    [DataMember(Name = "payer_msisdn")] public string PayerMsisdn { get; set; }

    [DataMember(Name = "channel_info")] public ChannelInfo ChannelInfo { get; set; }

    //todo bo sung truong nhu trong dac ta
    [DataMember(Name = "amount")] public int Amount { get; set; }

    [DataMember(Name = "trans_id")] public string TransId { get; set; }

    [DataMember(Name = "error_msg")] public string ErrorMsg { get; set; }

    [DataMember(Name = "error_code")] public string ErrorCode { get; set; }

    [DataMember(Name = "quantity")] public int Quantity { get; set; }

    [DataMember(Name = "original_order_id")]
    public string OriginalOrderId { get; set; }

    [DataMember(Name = "balance")] public decimal Balance { get; set; }
    [DataMember(Name = "acc_no")] public string AccNo { get; set; }
    [DataMember(Name = "bank_code")] public string BankCode { get; set; }

    [DataMember(Name = "billing_detail")] public string BillDetail { get; set; }

    [DataMember(Name = "reference_msg")] public string ReferenceMsg { get; set; }

    [DataMember(Name = "reference_code")] public string ReferenceCode { get; set; }

    [DataMember(Name = "reference_msg")] public string ReferenceMessage { get; set; }

    [DataMember(Name = "tpp_type")] public string TppType { get; set; }

    [DataMember(Name = "original_trans_id")]
    public string OriginalTransId { get; set; }
}

public class ChannelInfo
{
    [DataMember(Name = "channel_type")] public string ChannelType { get; set; }

    [DataMember(Name = "website_name")] public string WebsiteName { get; set; }

    [DataMember(Name = "website_address")] public string WebsiteAddress { get; set; }

    [DataMember(Name = "source")] public string Source { get; set; }

    [DataMember(Name = "acc_no")] public string AccNo { get; set; }

    [DataMember(Name = "bank_code")] public string BankCode { get; set; }
}