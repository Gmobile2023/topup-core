using System.Runtime.Serialization;

namespace HLS.Paygate.TopupGw.Components.Connectors.PayPoo;

public class PayPooPayRequest
{
    [DataMember(Name = "cmd")] public string Cmd { get; set; }

    [DataMember(Name = "data")] public DataObject Data { get; set; }

    [DataMember(Name = "signature")] public string Signature { get; set; }
}

public class PayPooPayCardRequest
{
    [DataMember(Name = "cmd")] public string Cmd { get; set; }

    [DataMember(Name = "data")] public DataCardObject Data { get; set; }

    [DataMember(Name = "signature")] public string Signature { get; set; }
}