﻿using System;
using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/card_batch", "PATCH")]
public class CardBatchUpdateRequest : IPatch, IReturn<MessageResponseBase>
{
    public Guid Id { get; set; }
    public string Vendor { get; set; }
    public string Description { get; set; }
    public byte Status { get; set; }
    public string Provider { get; set; }
}