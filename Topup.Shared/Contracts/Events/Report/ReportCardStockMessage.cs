using System;
using System.Collections.Generic;
using Orleans;

namespace Topup.Shared.Contracts.Events.Report;

public class ReportCardStockMessage
{
    public Guid Id { get; set; }
    public List<ProviderCardStockItem> ProviderItem { get; set; }
    public string StockCode { get; set; }
    public string ProductCode { get; set; }
    public string CategoryCode { get; set; }
    public string StockType { get; set; }
    public string Vendor { get; set; }
    public decimal CardValue { get; set; }
    public string InventoryType { get; set; }
    public int Increase { get; set; }
    public int Decrease { get; set; }
    public int Inventory { get; set; }
    public string Serial { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public int InventoryAfter{get;set;}
    public DateTime CreatedDate { get; set; }
}

[GenerateSerializer]
public class ProviderCardStockItem
{
    [Id(0)]
    public string ProviderCode { get; set; }

    [Id(1)]
    public string BatchCode { get; set; }
    [Id(2)]
    public int Quantity { get; set; }
}

public class ProviderCardStockDto
{   
    public string ProviderCode { get; set; }  
    public string BatchCode { get; set; }  
    public int Quantity { get; set; }
}

public static class CardTransType
{
    public static string Exchange = "Exchange";
    public static string Sale = "Sale";
    public static string Inventory = "Inventory";
    public static string Mapping = "Mapping";
    public static string Refund = "Refund";
}

public enum CardInventoryType : byte
{
    Increase = 1,
    Decrease = 0
}