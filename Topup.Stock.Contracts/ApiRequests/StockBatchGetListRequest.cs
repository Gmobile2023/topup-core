﻿using System;
using Topup.Shared;
using ServiceStack;
using Topup.Stock.Contracts.Enums;

namespace Topup.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/card_batches", "GET")]
public class StockBatchGetListRequest : PaggingBase, IGet, IReturn<MessagePagedResponseBase>
{
    public string Filter { get; set; }
    public string BatchCode { get; set; }
    public StockBatchStatus Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string Provider { get; set; }
    public string Vendor { get; set; }
    public string ImportType { get; set; }
}

[Route("/api/v1/stock/card_batches_available", "GET")]
public class CardBatchAvailableGetListRequest : PaggingBase, IGet, IReturn<MessagePagedResponseBase>
{
}