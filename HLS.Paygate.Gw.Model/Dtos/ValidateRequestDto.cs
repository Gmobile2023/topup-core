namespace HLS.Paygate.Gw.Model.Dtos;

public class ValidateRequestDto
{
    public string PartnerCode { get; set; }
    public string PlainText { get; set; }
    public string Signature { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
    public bool CheckProductCode { get; set; }
    public string SessionPartnerCode { get; set; }
    public string TransCode { get; set; }
    public int Quantity { get; set; }
}