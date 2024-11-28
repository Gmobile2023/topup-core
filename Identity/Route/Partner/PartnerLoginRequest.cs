using Identity.Models;
using ServiceStack;
using Topup.Shared;

namespace Identity.Route.Partner;

[Route("/api/v1/partner/id/access-token", "POST")]
public class PartnerLoginRequest : IPost, IReturn<NewMessageResponseBase<PartnerAuthResponse>>
{
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string Scope { get; set; }
    public string GrantType { get; set; }
}