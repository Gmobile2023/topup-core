using System;
using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/card_batch", "DELETE")]
public class CardBatchDeleteRequest : IDelete, IReturn<MessageResponseBase>
{
    public Guid Id { get; set; }
}