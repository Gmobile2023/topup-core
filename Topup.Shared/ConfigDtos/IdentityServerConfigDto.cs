namespace Topup.Shared.ConfigDtos;

public class IdentityServerConfigDto
{
    public OAuthIdentityServerDto IdentityServer { get; set; }
}

public class OAuthIdentityServerDto
{
    public string AuthorizeUrl { get; set; }

    public string Audience { get; set; }

    public string ClientId { get; set; }

    public string Scopes { get; set; }

    public string ClientSecret { get; set; }
}