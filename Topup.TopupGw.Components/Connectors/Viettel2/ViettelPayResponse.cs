﻿using System.Runtime.Serialization;

namespace Topup.TopupGw.Components.Connectors.Viettel2;

public class ViettelPayResponse
{
    [DataMember(Name = "data")] public DataObject Data { get; set; }

    [DataMember(Name = "signature")] public string Signature { get; set; }
}

public class ViettelCards
{
    [DataMember(Name = "service_code")] public string ServiceCode { get; set; }

    [DataMember(Name = "amount")] public string Amount { get; set; }

    [DataMember(Name = "serial")] public string Serial { get; set; }

    [DataMember(Name = "pincode")] public string Pincode { get; set; }

    [DataMember(Name = "expire")] public string Expire { get; set; }
}