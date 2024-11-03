using System;
using System.Collections.Generic;
using HLS.Paygate.Stock.Contracts.Enums;

namespace HLS.Paygate.Stock.Contracts.Dtos;

public class StockBatchDto
{
    public Guid Id { get; set; }
    public string BatchCode { get; set; }
    public string Description { get; set; }
    public StockBatchStatus Status { get; set; }

    public byte BatchStatus => (byte) Status;

    //public string Vendor { get; set; }
    public string Provider { get; set; }
    public string ImportType { get; set; }

    /// <summary>
    ///     Amount - giá vốn airtime
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    ///     Discount - chiết khấu nhập airtime
    /// </summary>
    public float Discount { get; set; }

    //public int Quantity { get; set; }
    /// <summary>
    ///     Airtime
    /// </summary>
    public decimal TotalValue { get; set; }

    public List<StockBatchItemDto> StockBatchItems { get; set; }

    public DateTime CreatedDate { get; set; }
    public string CreatedAccount { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string ModifiedAccount { get; set; }
    public DateTime? ExpiredDate { get; set; }    
}

public class StockBatchItemDto
{
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }

    /// <summary>
    ///     chiết khấu khi nhập thẻ
    /// </summary>
    public float Discount { get; set; }

    public int ItemValue { get; set; }
    public string ProductCode { get; set; }

    /// <summary>
    ///     Số lượng yêu cầu
    /// </summary>
    public int Quantity { get; set; }

    public int QuantityImport { get; set; }

    /// <summary>
    ///     giá vốn
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    ///     Airtime nhập
    /// </summary>
    public decimal Airtime { get; set; }

    public string TransCode { get; set; }
    public string TransCodeProvider { get; set; }

    public DateTime? ExpiredDate { get; set; }
}