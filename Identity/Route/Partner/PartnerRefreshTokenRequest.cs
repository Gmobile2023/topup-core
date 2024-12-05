using Identity.Models;
using ServiceStack;
using Topup.Shared;

namespace Identity.Route.Partner;

[Route("/api/v1/partner/id/refresh-token", "POST")]
public class PartnerRefreshTokenRequest : IPost, IReturn<NewMessageResponseBase<PartnerAuthResponse>>
{
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string RefreshToken { get; set; }
}