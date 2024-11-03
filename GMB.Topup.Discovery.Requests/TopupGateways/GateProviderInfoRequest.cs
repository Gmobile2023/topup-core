using System;
using System.Runtime.Serialization;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using ServiceStack;


namespace GMB.Topup.Discovery.Requests.TopupGateways
{
    [DataContract]
    [Route("/api/v1/gate_provider_info", "GET")]
    public class GateProviderInfoRequest : IGet, IReturn<NewMessageResponseBase<ProviderTopupInfoDto>>
    {
        [DataMember(Order = 1)] public string ProviderCode { get; set; }
    }
}
