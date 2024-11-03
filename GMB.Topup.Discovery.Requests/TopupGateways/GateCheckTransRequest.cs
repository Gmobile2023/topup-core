using System.Runtime.Serialization;
using GMB.Topup.Shared;
using ServiceStack;

namespace GMB.Topup.Discovery.Requests.TopupGateways;
[DataContract]
[Route("/api/v1/check_trans", "GET")]
public class GateCheckTransRequest : IGet, IReturn<NewMessageResponseBase<ResponseProvider>>
{
    [DataMember(Order = 1)] public string TransCodeToCheck { get; set; }
    [DataMember(Order = 2)] public string ProviderCode { get; set; }
    [DataMember(Order = 3)] public string ServiceCode { get; set; }
}