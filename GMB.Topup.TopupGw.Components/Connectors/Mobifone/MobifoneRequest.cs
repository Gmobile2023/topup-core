using System.Runtime.Serialization;
using System.Xml;

namespace GMB.Topup.TopupGw.Components.Connectors.Mobifone;

public class MobifoneRequest
{
    public const string Type_Topup = "topup";
    public const string Type_Pincode = "pincode";
    public const string Type_Balance = "balance";
    public const string Type_Topupdata = "topupdata";
    public const string Type_Checktrans = "checktrans";
    public const string Type_Airtime = "airtime";

    [DataMember(Name = "sessionid")] public string SessionId { get; set; }
    [DataMember(Name = "initiator")] public string Initiator { get; set; }
    [DataMember(Name = "pin")] public string Pin { get; set; }
    [DataMember(Name = "type")] public int Type { get; set; }
    [DataMember(Name = "amount")] public decimal Amount { get; set; }
    [DataMember(Name = "recipient")] public string Recipient { get; set; }
    [DataMember(Name = "reference1")] public string Reference1 { get; set; }
    [DataMember(Name = "reference2")] public string Reference2 { get; set; }
    [DataMember(Name = "target")] public string Target { get; set; }
    [DataMember(Name = "new_pin")] public string NewPin { get; set; }
    public string Account { get; set; }
    public string Password { get; set; }
    public string TransType { get; set; }
}