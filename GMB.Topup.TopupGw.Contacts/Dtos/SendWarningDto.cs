namespace GMB.Topup.TopupGw.Contacts.Dtos;

public class SendWarningDto
{
    public const string Topup = "TOPUP";
    public const string VBILL = "PAYBILL";
    public const string PinCode = "PINCODE";
    public string Type { get; set; }

    public string SendProviderFailed { get; set; }

    public string ProviderCode { get; set; }

    public string TransRef { get; set; }

    public string ReferenceCode { get; set; }

    public string PartnerCode { get; set; }

    public decimal TransAmount { get; set; }

    public string ReceiverInfo { get; set; }

    public string ProductCode { get; set; }

    public string TransCode { get; set; }

    public string Content { get; set; }

    public int Status { get; set; }
}