using System.Runtime.Serialization;

namespace Topup.TopupGw.Components.Connectors.Viettel2;

public class ViettelPayRequest
{
    [DataMember(Name = "cmd")] public string Cmd { get; set; }

    [DataMember(Name = "data")] public DataObject Data { get; set; }

    [DataMember(Name = "signature")] public string Signature { get; set; }
}

public class ViettelPayCardRequest
{
    [DataMember(Name = "cmd")] public string Cmd { get; set; }

    [DataMember(Name = "data")] public DataCardObject Data { get; set; }

    [DataMember(Name = "signature")] public string Signature { get; set; }
}