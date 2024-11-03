namespace HLS.Paygate.TopupGw.Components.Connectors.ZoTa;

public class ZoTaRequest
{
    public string Username { get; set; }
    public string ApiCode { get; set; }
    public string ApiUsername { get; set; }
    public string RequestId { get; set; }
    public string DataSign { get; set; }
    public string TelcoType { get; set; }
    public string TelcoServiceType { get; set; }
    public decimal Amount { get; set; }
    public string Msisdn { get; set; }
    public string CardType { get; set; }
    public string FaceValue { get; set; }
    public int Quantity { get; set; }
    public string ServiceCode { get; set; }
    public string InvoiceReference { get; set; }
    public string CustomerReference { get; set; }
    public bool PayAll { get; set; }
    public string ReferenceId { get; set; }
    public string TxnId { get; set; }
}