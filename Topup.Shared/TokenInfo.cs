using System;

namespace Topup.Shared;

public class TokenInfo
{
    public string Token { get; set; }
    public string ProviderCode { get; set; }
    public DateTime RequestDate { get; set; }
}