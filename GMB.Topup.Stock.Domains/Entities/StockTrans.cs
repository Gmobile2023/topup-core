using System;
using System.Collections.Generic;
using GMB.Topup.Stock.Contracts.Enums;
using MongoDbGenericRepository.Models;

namespace GMB.Topup.Stock.Domains.Entities;

public class StockTrans : Document
{
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
    public List<StockTransItem> StockItemsRequest { get; set; }
    public List<StockTransItem> StockTransItems { get; set; }
}

public class StockTransItem
{
    public int ItemValue { get; set; }
    public string ProductCode { get; set; }
    public int Quantity { get; set; }
}