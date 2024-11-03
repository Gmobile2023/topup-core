using System;
using GMB.Topup.Stock.Contracts.Enums;
using GMB.Topup.Shared;
using ServiceStack;

namespace GMB.Topup.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/card/status", "PATCH")]
public class CardUpdateStatus : IPatch, IReturn<MessageResponseBase>
{
    public Guid Id { get; set; }
    public CardStatus CardStatus { get; set; }
}