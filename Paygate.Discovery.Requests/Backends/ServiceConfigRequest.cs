using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Shared;
using ServiceStack;

namespace Paygate.Discovery.Requests.Backends;

[Route("/api/v1/service", "GET")]
public class GetServiceRequest : IGet, IReturn<NewMessageReponseBase<ServiceConfigDto>>
{
    public string PartnerCode { get; set; }
}

[Route("/api/v1/service/create-update", "POST")]
public class CreateOrUpdateServiceRequest : IPost, IReturn<NewMessageReponseBase<object>>
{
    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
}