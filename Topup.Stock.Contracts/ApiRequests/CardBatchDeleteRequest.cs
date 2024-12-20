﻿using System;
using Topup.Shared;
using ServiceStack;

namespace Topup.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/card_batch", "DELETE")]
public class CardBatchDeleteRequest : IDelete, IReturn<MessageResponseBase>
{
    public Guid Id { get; set; }
}