using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using ServiceStack;

namespace GMB.Topup.Discovery.Requests.TopupGateways;

[DataContract]
[Route("/api/v1/card", "POST")]
public class GateCardRequest : IPost, IReturn<NewMessageResponseBase<List<CardRequestResponseDto>>>
{
    [DataMember(Order = 1)] public DateTime RequestDate { get; set; }
    [DataMember(Order = 2)] public int Quantity { get; set; }
    [DataMember(Order = 3)] public decimal Amount { get; set; }
    [DataMember(Order = 4)] public string Vendor { get; set; }
    [DataMember(Order = 5)] public string TransRef { get; set; }
    [DataMember(Order = 6)] public string ProviderCode { get; set; }
    [DataMember(Order = 7)] public string ProductCode { get; set; }
    [DataMember(Order = 8)] public string ReferenceCode { get; set; }
    [DataMember(Order = 9)] public string TransCodeProvider { get; set; }
}