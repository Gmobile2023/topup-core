namespace Identity.Models;

public class PartnerAuthResponse
{
    public string AccessToken { get; set; }
    public int ExpiresIn { get; set; }
    public string IdToken { get; set; }
    public string RefreshToken { get; set; }
}