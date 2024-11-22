using System;
using Topup.Shared;
using ServiceStack;

namespace Topup.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/card", "PATCH")]
public class CardUpdateRequest : IPatch, IReturn<MessageResponseBase>
{
    public Guid Id { get; set; }
    public string Serial { get; set; }
    public string CardCode { get; set; }
    public DateTime ExpiredDate { get; set; }
}