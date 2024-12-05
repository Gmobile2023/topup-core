namespace Topup.Shared.Dtos;

public class PartnerResult
{
    public string TransCode { get; set; }
    public string RequestCode { get; set; }
    public decimal? PaymentAmount { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Discount { get; set; }
    public string ProviderType { get; set; }
}