using System.Runtime.Serialization;

namespace Topup.Shared.Dtos;

[DataContract]
public class IdentityAuthResponse
{
    [DataMember(Name = "access_token")] public string AccessToken { get; set; }

    [DataMember(Name = "expires_in")] public int ExpiresIn { get; set; }

    [DataMember(Name = "token_type")] public string TokenType { get; set; }

    [DataMember(Name = "scope")] public string Scope { get; set; }

    [DataMember(Name = "refresh_token")] public string RefreshToken { get; set; }

    [DataMember(Name = "id_token")] public string IdToken { get; set; }
}