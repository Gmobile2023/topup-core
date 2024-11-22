using System.Runtime.Serialization;
using Topup.Shared;
using ServiceStack;

namespace Topup.Discovery.Requests.Tool;

[Route("/api/v1/mobile/mobile_info", "GET")]
[DataContract]
public class MobileInfoRequest : IGet, IReturn<NewMessageResponseBase<string>>
{
    [DataMember(Order = 1)] public string ProviderCode { get; set; }

    [DataMember(Order = 2)] public string Telco { get; set; }

    [DataMember(Order = 3)] public string TransCode { get; set; }

    [DataMember(Order = 4)] public string MsIsdn { get; set; }
}