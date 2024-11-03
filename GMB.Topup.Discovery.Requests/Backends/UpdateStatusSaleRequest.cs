using System.Runtime.Serialization;
using GMB.Topup.Shared;
using ServiceStack;

namespace GMB.Topup.Discovery.Requests.Backends;

[DataContract]
[Route("/api/v1/topup/status", "PATCH")]
public class TopupUpdateStatusRequest : IPatch, IReturn<MessageResponseBase>
{
    [DataMember(Order = 1)] public SaleRequestStatus Status { get; set; }
    [DataMember(Order = 2)] public string TransCode { get; set; }
}