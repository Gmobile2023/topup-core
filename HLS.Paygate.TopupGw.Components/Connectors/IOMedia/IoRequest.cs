namespace HLS.Paygate.TopupGw.Components.Connectors.IOMedia;

public class IoRequest
{
    public string PartnerCode { get; set; }
    public string PartnerTransId { get; set; }
    public string ProductCode { get; set; }
    public string TelcoCode { get; set; }
    public string MobileNo { get; set; }
    public long ? TopupAmount { get; set; }
    public string TransType { get; set; }
    public string BillingCode { get; set; }
    public long ? PaidAmount { get; set; }
    public int? Quantity { get; set; }
    public string Reciever { get; set; }

    public string Sign { get; set; }
}