using System;

namespace GMB.Topup.Report.Model.Dtos.ResponseDto;

public class ReportCardStockDto
{
    public Guid Id { get; set; }
    public string StockCode { get; set; }
    public string ProductCode { get; set; }
    public string StockType { get; set; }
    public string CategoryCode { get; set; }
    public int Inventory { get; set; }
    public int InventoryLimit { get; set; }
    public int MinimumInventoryLimit { get; set; }
    public string Description { get; set; }

    public decimal CardValue { get; set; }
    //public string Vendor { get; set; }
}