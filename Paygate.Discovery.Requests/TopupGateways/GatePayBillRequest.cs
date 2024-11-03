using System;
using System.Runtime.Serialization;
using HLS.Paygate.Shared;
using ServiceStack;

namespace Paygate.Discovery.Requests.TopupGateways;

[DataContract]
[Route("/api/v1/bill", "POST")]
public class GatePayBillRequest : IPost, IReturn<NewMessageReponseBase<ResponseProvider>>
{
    [DataMember(Order = 1)] public string ServiceCode { get; set; }
    [DataMember(Order = 2)] public string CategoryCode { get; set; }
    [DataMember(Order = 3)] public string Vendor { get; set; }
    [DataMember(Order = 4)] public decimal Amount { get; set; }
    [DataMember(Order = 5)] public string ReceiverInfo { get; set; }
    [DataMember(Order = 6)] public string TransRef { get; set; }
    [DataMember(Order = 7)] public DateTime RequestDate { get; set; }
    [DataMember(Order = 8)] public string ProviderCode { get; set; }
    [DataMember(Order = 9)] public string ProductCode { get; set; }
    [DataMember(Order = 10)] public bool IsInvoice { get; set; }
    [DataMember(Order = 11)] public string PartnerCode { get; set; }
    [DataMember(Order = 12)] public string ReferenceCode { get; set; }
    [DataMember(Order = 13)] public string TransCodeProvider { get; set; }
    [DataMember(Order = 14)] public string Info { get; set; }
}