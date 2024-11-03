using System;
using GMB.Topup.Shared;
using ServiceStack;

namespace GMB.Topup.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/card_batch", "POST")]
public class CardBatchCreateRequest : IPost, IReturn<MessageResponseBase>
{
    public DateTime CreatedDate { get; set; }
    public string BatchCode { get; set; }

    /// <summary>
    ///     viettel, vina, mobi
    /// </summary>
    public string Vendor { get; set; }

    public string Description { get; set; }
    public byte Status { get; set; }

    /// <summary>
    ///     Từ ncc nào, mobi9, zp,....
    /// </summary>
    public string Provider { get; set; }
}