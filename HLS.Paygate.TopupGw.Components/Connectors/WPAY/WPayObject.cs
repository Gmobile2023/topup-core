using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HLS.Paygate.TopupGw.Components.Connectors.WPay;

public class WPayRequest
{
    [DataMember(Name = "partnerCode")] public string partnerCode { get; set; }

    [DataMember(Name = "partnerTransId")] public string partnerTransId { get; set; }

    [DataMember(Name = "productCode")] public string productCode { get; set; }

    [DataMember(Name = "quantity")] public int? quantity { get; set; }

    [DataMember(Name = "telcoCode")] public string telcoCode { get; set; }

    [DataMember(Name = "mobileNo")] public string mobileNo { get; set; }

    [DataMember(Name = "topupAmount")] public long? topupAmount { get; set; }

    [DataMember(Name = "transType")] public string transType { get; set; }

    [DataMember(Name = "billingCode")] public string billingCode { get; set; }

    [DataMember(Name = "paidAmount")] public long? paidAmount { get; set; }


    [DataMember(Name = "sign")] public string sign { get; set; }
}

public class WPayReponse
{
    [DataMember(Name = "resCode")] public string resCode { get; set; }

    [DataMember(Name = "resMessage")] public string resMessage { get; set; }

    [DataMember(Name = "partnerCode")] public string partnerCode { get; set; }

    [DataMember(Name = "partnerTransId")] public string partnerTransId { get; set; }
    [DataMember(Name = "mobileType")] public string mobileType { get; set; }

    [DataMember(Name = "status")] public int status { get; set; }

    [DataMember(Name = "currentBalance")] public long currentBalance { get; set; }

    [DataMember(Name = "billingName")] public string billingName { get; set; }

    [DataMember(Name = "amount")] public long amount { get; set; }

    public List<WPayCardInfo> cardList { get; set; }

    [DataMember(Name = "sign")] public string sign { get; set; }
}

public class WPayCardInfo
{
    public string serial_code { get; set; }

    public string pin_code { get; set; }

    public string expire_date { get; set; }
}