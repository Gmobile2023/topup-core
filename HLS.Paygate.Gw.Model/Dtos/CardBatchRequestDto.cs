using System;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Gw.Model.Dtos;

[Serializable]
public class CardBatchRequestDto
{
    public CardBatchRequestDto()
    {
        CreatedTime = DateTime.Now;
    }

    public int Quantity { get; set; }

    public decimal Amount { get; set; }

    public decimal Price { get; set; }
    public decimal? DiscountRate { get; set; }
    public decimal? DiscountAmount { get; set; }
    public CardBatchRequestStatus Status { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime RequestDate { get; set; }
    public DateTime? EndDate { get; set; }

    public Channel Channel { get; set; }
    public string TransCode { get; set; }
    public string Provider { get; set; }

    public string Vendor { get; set; }
    public string ServiceCode { get; set; }
    public string ProductCode { get; set; }
    public string CategoryCode { get; set; }
    public string ExtraInfo { get; set; }
    public string ProviderTransCode { get; set; }
    public string RequestIp { get; set; }

    public string UserProcess { get; set; }
}