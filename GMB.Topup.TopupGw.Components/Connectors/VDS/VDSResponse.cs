﻿using System.Runtime.Serialization;

namespace GMB.Topup.TopupGw.Components.Connectors.VDS;

public class VDSResponse
{
    [DataMember(Name = "data")] public DataObject Data { get; set; }

    [DataMember(Name = "signature")] public string Signature { get; set; }
}

public class VDSCards
{
    [DataMember(Name = "service_code")] public string ServiceCode { get; set; }

    [DataMember(Name = "amount")] public string Amount { get; set; }

    [DataMember(Name = "serial")] public string Serial { get; set; }

    [DataMember(Name = "pincode")] public string Pincode { get; set; }

    [DataMember(Name = "expire")] public string Expire { get; set; }
}