using System;
using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/card", "PATCH")]
public class CardUpdateRequest : IPatch, IReturn<MessageResponseBase>
{
    public Guid Id { get; set; }
    public string Serial { get; set; }
    public string CardCode { get; set; }
    public DateTime ExpiredDate { get; set; }
}