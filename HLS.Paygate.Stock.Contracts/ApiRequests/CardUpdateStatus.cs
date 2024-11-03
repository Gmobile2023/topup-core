using System;
using HLS.Paygate.Shared;
using HLS.Paygate.Stock.Contracts.Enums;
using ServiceStack;

namespace HLS.Paygate.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/card/status", "PATCH")]
public class CardUpdateStatus : IPatch, IReturn<MessageResponseBase>
{
    public Guid Id { get; set; }
    public CardStatus CardStatus { get; set; }
}