using System.Collections.Generic;
using ServiceStack;

namespace HLS.Paygate.Shared.Authentication;

[Route("/identity")]
public class GetIdentity : IReturn<GetIdentityResponse>
{
}

public class GetIdentityResponse
{
    public List<Property> Claims { get; set; }
    public AuthUserSession Session { get; set; }
}