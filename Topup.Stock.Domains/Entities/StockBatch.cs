using System;
using System.Collections.Generic;
using Topup.Stock.Contracts.Enums;
using MongoDbGenericRepository.Models;

namespace Topup.Stock.Domains.Entities;

public class StockBatch : Document
{
    public string BatchCode { get; set; }
    public string Description { get; set; }
    public StockBatchStatus Status { get; set; }

    /// <summary>
    ///     Vendor = VTE_PINCODE, VTE_DATA, ZING_PINCODE, ZING_DATA
    /// </summary>
    public string Vendor { get; set; }

    public string Provider { get; set; }
    public string ImportType { get; set; }
    public float Discount { get; set; }

    /// <summary>
    ///     Airtime
    /// </summary>
    public decimal TotalValue { get; set; }

    /// <summary>
    ///     giá vốn
    /// </summary>
    public decimal Amount { get; set; }

    public DateTime CreatedDate { get; set; }
    public string CreatedAccount { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string ModifiedAccount { get; set; }

    public List<StockBatchItem> StockBatchItems { get; set; }
    // public int? Quantity { get; set; }
    // public int? Value { get; set; } 

    public DateTime? ExpiredDate { get; set; }
}

public class StockBatchItem
{
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }

    /// <summary>
    ///     chiết khấu khi nhập thẻ
    /// </summary>
    public float Discount { get; set; }

    public decimal ItemValue { get; set; }
    public string ProductCode { get; set; }
    public int Quantity { get; set; }
    public int QuantityImport { get; set; }

    /// <summary>
    ///     tổng tiền
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    ///     giá trị  Airtime
    /// </summary>
    public decimal Airtime { get; set; }
}