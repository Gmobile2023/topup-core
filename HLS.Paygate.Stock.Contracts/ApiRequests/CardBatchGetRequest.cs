using System;
using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/card_batch", "GET")]
public class CardBatchGetRequest : IGet, IReturn<MessageResponseBase>
{
    public Guid Id { get; set; }
    public string BatchCode { get; set; }
}