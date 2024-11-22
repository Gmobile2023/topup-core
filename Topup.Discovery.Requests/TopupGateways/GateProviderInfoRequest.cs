using System;
using System.Runtime.Serialization;
using Topup.Shared;
using Topup.Shared.Dtos;
using ServiceStack;


namespace Topup.Discovery.Requests.TopupGateways
{
    [DataContract]
    [Route("/api/v1/gate_provider_info", "GET")]
    public class GateProviderInfoRequest : IGet, IReturn<NewMessageResponseBase<ProviderTopupInfoDto>>
    {
        [DataMember(Order = 1)] public string ProviderCode { get; set; }
    }
}
