using Topup.Gw.Model.Dtos;
using Topup.Shared;
using ServiceStack;

namespace Topup.Discovery.Requests.Backends;

[Route("/api/v1/service", "GET")]
public class GetServiceRequest : IGet, IReturn<NewMessageResponseBase<ServiceConfigDto>>
{
    public string PartnerCode { get; set; }
}

[Route("/api/v1/service/create-update", "POST")]
public class CreateOrUpdateServiceRequest : IPost, IReturn<NewMessageResponseBase<object>>
{
    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
}