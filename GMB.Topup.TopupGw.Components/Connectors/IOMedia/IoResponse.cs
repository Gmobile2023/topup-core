using System.Collections.Generic;

namespace GMB.Topup.TopupGw.Components.Connectors.IOMedia;

public class IoResponse
{
    public string ResCode { get; set; }
    public string ResMessage { get; set; }
    public long CurrentBalance { get; set; }
    public string PartnerCode { get; set; }
    public string PartnerTransId { get; set; }
    public long TotalValue { get; set; }
    public long DiscountValue { get; set; }
    public long DebitValue { get; set; }
    public string MobileType { get; set; }
    public string BillingName { get; set; }
    public long Amount { get; set; }
    public int TransStatus { get; set; }
    public List<CardInfo> CardList { get; set; }
    public string Sign { get; set; }
}

public class CardInfo
{
    public string Serial { get; set; }
    public string Pincode { get; set; }
    public string Expiredate { get; set; }
}