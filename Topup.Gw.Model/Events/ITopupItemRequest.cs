namespace HLS.Paygate.Gw.Model.Events
{
    public interface ITopupItemRequest
    {
        string TopupTransCode { get; set; }
        string CardTransCode { get; set; }
        int Amount { get; set; }
        byte Status { get; set; }
        string TopupType { get; set; }
        string Vendor { get; set; }
        string ShortCode { get; set; }
    }
}