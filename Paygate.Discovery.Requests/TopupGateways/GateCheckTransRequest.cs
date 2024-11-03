using System.Runtime.Serialization;
using HLS.Paygate.Shared;
using ServiceStack;

namespace Paygate.Discovery.Requests.TopupGateways;
[DataContract]
[Route("/api/v1/check_trans", "GET")]
public class GateCheckTransRequest : IGet, IReturn<NewMessageReponseBase<ResponseProvider>>
{
    [DataMember(Order = 1)] public string TransCodeToCheck { get; set; }
    [DataMember(Order = 2)] public string ProviderCode { get; set; }
    [DataMember(Order = 3)] public string ServiceCode { get; set; }
}