using System.Runtime.Serialization;

namespace Topup.TopupGw.Components.Connectors.VDS;

public class VDSRequest
{
    [DataMember(Name = "cmd")] public string Cmd { get; set; }

    [DataMember(Name = "data")] public DataObject Data { get; set; }

    [DataMember(Name = "signature")] public string Signature { get; set; }
}

public class VDSCardRequest
{
    [DataMember(Name = "cmd")] public string Cmd { get; set; }

    [DataMember(Name = "data")] public DataCardObject Data { get; set; }

    [DataMember(Name = "signature")] public string Signature { get; set; }
}