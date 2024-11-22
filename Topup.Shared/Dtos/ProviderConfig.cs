namespace Topup.Shared.Dtos;

public class ProviderConfig
{
    public int Priority { get; set; }
    public string ProviderCode { get; set; }

    public string TransCodeConfig { get; set; }
    public bool MustCount { get; set; }

    public int? ProviderMaxWaitingTimeout { get; set; }

    public int? ProviderSetTransactionTimeout { get; set; }
    public string StatusResponseWhenJustReceived { get; set; }
    public int? WaitingTimeResponseWhenJustReceived { get; set; }

    public bool IsEnableResponseWhenJustReceived { get; set; }
}