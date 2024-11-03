namespace HLS.Paygate.Shared.ConfigDtos;

public class IdentityServerConfigDto
{
    public OAuthIdentityServerDto IdentityServer { get; set; }
}

public class OAuthIdentityServerDto
{
    public string AuthorizeUrl { get; set; }
    public string Audience { get; set; }
}