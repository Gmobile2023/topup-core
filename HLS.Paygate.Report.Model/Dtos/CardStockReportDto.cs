using System;

namespace HLS.Paygate.Report.Model.Dtos;

public class ReportCardStockByDateDto
{
    public string StockCode { get; set; }
    public int CardValue { get; set; }
    public string Vendor { get; set; }
    public int Increase { get; set; }
    public int Decrease { get; set; }
    public int InventoryBefore { get; set; }
    public int InventoryAfter { get; set; }
    public byte Status { get; set; }
    public string ModifiedBy { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; }
    public string ProductCode { get; set; }
    public string StockType { get; set; }
    public string CategoryCode { get; set; }
}

public class ReportCardStockHistoriesDto
{
    public string StockCode { get; set; }
    public int CardValue { get; set; }
    public string Serial { get; set; }
    public string Vendor { get; set; }
    public int Increase { get; set; }
    public int Decrease { get; set; }
    public int InventoryBefore { get; set; }
    public int InventoryAfter { get; set; }
    public byte Status { get; set; }
    public string ModifiedBy { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; }
    public string PartnerCode { get; set; }
    public string TransType { get; set; }
    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public string InventoryType { get; set; }
    public string ProductCode { get; set; }
    public string StockType { get; set; }
    public string CategoryCode { get; set; }
}