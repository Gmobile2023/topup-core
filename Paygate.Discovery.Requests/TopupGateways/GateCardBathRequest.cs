using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using ServiceStack;

namespace Paygate.Discovery.Requests.TopupGateways;

[DataContract]
public class GateCardBathToStockRequest : IPost, IReturn<NewMessageReponseBase<List<CardRequestResponseDto>>>
{
    [DataMember(Order = 1)] public string ServiceCode { get; set; }
    [DataMember(Order = 2)] public string CategoryCde { get; set; }
    [DataMember(Order = 3)] public string Vendor { get; set; }
    [DataMember(Order = 4)] public decimal Amount { get; set; }
    [DataMember(Order = 5)] public int Quantity { get; set; }
    [DataMember(Order = 6)] public string ProductCode { get; set; }
    [DataMember(Order = 7)] public string TransRef { get; set; }
    [DataMember(Order = 8)] public DateTime RequestDate { get; set; }
    [DataMember(Order = 9)] public string ProviderCode { get; set; }

    //public bool AutoImportToStock { get; set; }
    [DataMember(Order = 10)] public string PartnerCode { get; set; }
    [DataMember(Order = 11)] public string ReferenceCode { get; set; }
    [DataMember(Order = 12)] public string TransCodeProvider { get; set; }
}