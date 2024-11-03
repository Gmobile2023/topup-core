using System.Collections.Generic;
using System.Runtime.Serialization;
using ServiceStack;

namespace GMB.Topup.Shared;

[DataContract]
public class CustomUserSession : AuthUserSession
{
    [DataMember] public string AccountCode { get; set; }
    [DataMember] public string ClientId { get; set; }
}

[Route("/servicestack-identity")]
public class GetIdentity : IReturn<GetIdentityResponse>
{
}

public class GetIdentityResponse
{
    public List<Property> Claims { get; set; }
    public AuthUserSession Session { get; set; }
}