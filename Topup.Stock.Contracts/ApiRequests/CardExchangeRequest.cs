using System;
using System.Collections.Generic;
using Topup.Shared;
using MassTransit;
using ServiceStack;
using Topup.Stock.Contracts.Dtos;

namespace Topup.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/cards_exchange", "POST")]
public class ExchangeRequest : IPost, IReturn<MessageResponseBase>
{
    [ApiMember(ExcludeInSchema = false, IsRequired = true, Description = "Mã kho nguồn")]
    public string SrcStockCode { get; set; }

    [ApiMember(ExcludeInSchema = false, IsRequired = true, Description = "Mã kho đích")]
    public string DesStockCode { get; set; }

    [ApiMember(ExcludeInSchema = false, IsRequired = true, Description = "Trạng thái thẻ muốn chuyển")]
    public byte Status { get; set; }

    [ApiMember(ExcludeInSchema = false, IsRequired = true, Description = "Số lượng")]
    public int Quantity { get; set; }

    [ApiMember(ExcludeInSchema = false, IsRequired = true, Description = "Mã kho lô")]
    public string BatchCode { get; set; }

    [ApiMember(ExcludeInSchema = false, IsRequired = true, Description = "Mệnh giá")]
    public int? CardValue { get; set; }

    [ApiMember(ExcludeInSchema = false, IsRequired = true, Description = "Vendor ")]
    public string Vendor { get; set; }

    //[ApiMember(ExcludeInSchema = true)]
    public Guid CorrelationId => NewId.NextGuid();

    [ApiMember(ExcludeInSchema = false, Description = "Mô tả")]
    public string Description { get; set; }
}

[Route("/api/v1/stock/get-card-transfer-info", "GET")]
public class GetCardInfoTransferRequest : IGet, IReturn<MessageResponseBase>
{
    public string SrcStockCode { get; set; }
    public string DesStockCode { get; set; }
    public string TransferType { get; set; }
    public string BatchCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
}

[Route("/api/v1/stock/transfer-card", "POST")]
public class StockTransferCardRequest : IPost, IReturn<MessageResponseBase>
{
    public string SrcStockCode { get; set; }
    public string DesStockCode { get; set; }
    public string TransferType { get; set; }
    public string BatchCode { get; set; }
    public List<StockTransferItemInfo> ProductList { get; set; }
}