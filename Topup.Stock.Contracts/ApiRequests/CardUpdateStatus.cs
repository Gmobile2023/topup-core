using System;
using Topup.Shared;
using ServiceStack;
using Topup.Stock.Contracts.Enums;

namespace Topup.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/card/status", "PATCH")]
public class CardUpdateStatus : IPatch, IReturn<MessageResponseBase>
{
    public Guid Id { get; set; }
    public CardStatus CardStatus { get; set; }
}