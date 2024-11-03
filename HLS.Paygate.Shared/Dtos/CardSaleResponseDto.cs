using System;
using System.Runtime.Serialization;

namespace HLS.Paygate.Shared.Dtos;
[DataContract]
public class CardRequestResponseDto
{
    [DataMember(Order = 1)] public string ExpireDate { get; set; }
    [DataMember(Order = 2)] public DateTime ExpiredDate { get; set; }
    [DataMember(Order = 3)] public string CardCode { get; set; }
    [DataMember(Order = 4)] public string Serial { get; set; }
    [DataMember(Order = 5)] public string CardValue { get; set; }
    [DataMember(Order = 6)] public string CardType { get; set; }
}

[DataContract]
public class CardItemsImport
{
    [DataMember(Order = 1)] public string CardCode { get; set; }
    [DataMember(Order = 2)] public string Serial { get; set; }
    [DataMember(Order = 3)] public string ExpiredDate { get; set; }
    [DataMember(Order = 4)] public decimal CardValue { get; set; }
}

[DataContract]
public class CardResponsePartnerDto
{
    [DataMember(Order = 1)] public string ExpireDate { get; set; }
    [DataMember(Order = 2)] public string CardCode { get; set; }
    [DataMember(Order = 3)] public string Serial { get; set; }
    [DataMember(Order = 4)] public string CardValue { get; set; }
}


public class CardProviderItemWaitDto
{
    public string ProductCode { get; set; }
    public string ServiceCode { get; set; }
    public string TransCodeProvider { get; set; }
}