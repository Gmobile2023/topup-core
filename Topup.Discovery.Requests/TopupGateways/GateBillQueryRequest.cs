using Topup.Shared;
using Topup.Shared.Dtos;
using ServiceStack;
using System.Runtime.Serialization;

namespace Topup.Discovery.Requests.TopupGateways;

[Route("/api/v1/bill", "GET")]
[DataContract]
public class GateBillQueryRequest : IGet, IReturn<NewMessageResponseBase<InvoiceResultDto>>
{
    [DataMember(Order = 1)] public string ReceiverInfo { get; set; }
    [DataMember(Order = 2)] public bool IsInvoice { get; set; }
    [DataMember(Order = 3)] public string TransRef { get; set; }
    [DataMember(Order = 4)] public string ProductCode { get; set; }
    [DataMember(Order = 5)] public string ServiceCode { get; set; }
    [DataMember(Order = 6)] public string CategoryCode { get; set; }
    [DataMember(Order = 7)] public string Vendor { get; set; }
    [DataMember(Order = 8)] public string ProviderCode { get; set; }
}