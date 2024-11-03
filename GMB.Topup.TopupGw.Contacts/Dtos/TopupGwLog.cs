namespace GMB.Topup.TopupGw.Contacts.Dtos;

public class TopupGwLog
{
    public string TransCode { get; set; }
    public string TransRef { get; set; }
    public string ProviderCode { get; set; }
    public string ProductCode { get; set; }
    public System.DateTime TransDate { get; set; }
    public string TransIndex { get; set; }
}