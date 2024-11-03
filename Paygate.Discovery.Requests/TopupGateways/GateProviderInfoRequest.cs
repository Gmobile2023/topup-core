using System;
using System.Runtime.Serialization;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using ServiceStack;


namespace Paygate.Discovery.Requests.TopupGateways
{
    [DataContract]
    [Route("/api/v1/gate_provider_info", "GET")]
    public class GateProviderInfoRequest : IGet, IReturn<NewMessageReponseBase<ProviderTopupInfoDto>>
    {
        [DataMember(Order = 1)] public string ProviderCode { get; set; }
    }
}
