using System;
using System.Collections.Generic;
using GMB.Topup.Stock.Contracts.Enums;

namespace GMB.Topup.Stock.Contracts.Dtos;

public class StockTransDto
{
    public Guid Id { get; set; }
    public string StockTransCode { get; set; }
    public DateTime CreatedDate { get; set; }
    public StockTransStatus Status { get; set; }
    public string StockTransType { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string SrcStockCode { get; set; }
    public string DesStockCode { get; set; }
    public string ProviderCode { get; set; }
    public int Quantity { get; set; }
    public string TransRef { get; set; }
    public int? CardValue { get; set; }
    public string Vendor { get; set; }
    public string Description { get; set; }
    public List<StockTransItemDto> StockItemsRequest { get; set; }
    public List<StockTransItemDto> StockTransItems { get; set; }
}

public class StockTransItemDto
{
    public decimal ItemValue { get; set; }
    public string ProductCode { get; set; }

    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }
    public string CategoryCode { get; set; }
    public string CategoryName { get; set; }

    public string ProductName { get; set; }
    public decimal CardValue { get; set; }
    public int QuantityAvailable { get; set; }
    public int Quantity { get; set; }
}

public class StockTransferItemInfo
{
    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }
    public string CategoryCode { get; set; }
    public string CategoryName { get; set; }
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public decimal CardValue { get; set; }
    public int QuantityAvailable { get; set; }
    public int Quantity { get; set; }
}