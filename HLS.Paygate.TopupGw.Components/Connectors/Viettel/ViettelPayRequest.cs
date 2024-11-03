namespace HLS.Paygate.TopupGw.Components.Connectors.Viettel;

public class ViettelPayRequest
{
    public string Command { get; set; }
    public string Data { get; set; }
    public string Sign { get; set; }
}