using System.Runtime.Serialization;
using HLS.Paygate.Shared;
using ServiceStack;

namespace Paygate.Discovery.Requests.TopupGateways;

[DataContract]
[Route("/api/v1/check_msisdn", "POST")]
public class GateCheckMsIsdnRequest : IPost, IReturn<NewMessageReponseBase<string>>
{
    [DataMember(Order = 1)]
    public string ProviderCode { get; set; }
    [DataMember(Order = 2)]
    public string Telco { get; set; }
    [DataMember(Order = 3)]
    public string TransCode { get; set; }
    [DataMember(Order = 4)]
    public string MsIsdn { get; set; }
}