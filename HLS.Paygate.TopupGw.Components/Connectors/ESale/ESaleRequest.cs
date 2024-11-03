using System.Runtime.Serialization;

namespace HLS.Paygate.TopupGw.Components.Connectors.ESale;

[DataContract()]
class GetBalanceRequest
{
    [DataMember(Name = "transId")]
    public string TransId { get; set; }

    [DataMember(Name = "agencyCode")]
    public string AgencyCode { get; set; }

    [DataMember(Name = "clientCode")]
    public string ClientCode { get; set; }

    [DataMember(Name = "time")]
    public string Time { get; set; }

    [DataMember(Name = "sig")]
    public string Sig { get; set; }
}

[DataContract()]
class CardRequest
{
    [DataMember(Name = "transId")]
    public string TransId { get; set; }

    [DataMember(Name = "agencyCode")]
    public string AgencyCode { get; set; }

    [DataMember(Name = "clientCode")]
    public string ClientCode { get; set; }

    [DataMember(Name = "supplierCode")]
    public string SupplierCode { get; set; }

    [DataMember(Name = "cardId")]
    public int CardId { get; set; }
    [DataMember(Name = "quantity")]
    public int Quantity { get; set; }

    [DataMember(Name = "transactionDate")]
    public string TransactionDate { get; set; }

    [DataMember(Name = "time")]
    public string Time { get; set; }

    [DataMember(Name = "checkSum")]
    public string checkSum { get; set; }

    [DataMember(Name = "signature")]
    public string Signature { get; set; }
}

[DataContract()]
class CheckTransRequest
{
    [DataMember(Name = "transId")]
    public string TransId { get; set; }

    [DataMember(Name = "agencyCode")]
    public string AgencyCode { get; set; }

    [DataMember(Name = "clientCode")]
    public string ClientCode { get; set; }

    [DataMember(Name = "transactionDate")]
    public string TransDate { get; set; }
    [DataMember(Name = "isGetCard")]

    public int IsGetCard { get; set; }

    [DataMember(Name = "time")]
    public string Time { get; set; }

    [DataMember(Name = "checkSum")]
    public string CheckSum { get; set; }

    [DataMember(Name = "signature")]
    public string Signature { get; set; }
}





