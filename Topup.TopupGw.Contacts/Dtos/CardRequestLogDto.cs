using System;
using Topup.TopupGw.Contacts.Enums;

namespace Topup.TopupGw.Contacts.Dtos;

public class CardRequestLogDto : DocumentDto
{
    public string TransCode { get; set; }
    public string TransRef { get; set; }
    public decimal TransAmount { get; set; }
    public string ReceiverInfo { get; set; }
    public TransRequestStatus Status { get; set; }
    public DateTime RequestDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public int Quantity { get; set; }
    public string Vendor { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
    public bool PayAll => true;
    public string ProviderCode { get; set; }
    public string ResponseInfo { get; set; }
    public string ServiceCode { get; set; }
    public string PartnerCode { get; set; }
    public string ReferenceCode { get; set; }
    public string TransIndex { get; set; }
}