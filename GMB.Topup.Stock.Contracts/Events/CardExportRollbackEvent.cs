using System.Collections.Generic;
using GMB.Topup.Stock.Contracts.Dtos;

namespace GMB.Topup.Stock.Contracts.Events;

public record CardExportRollbackEvent
{
    public string StockCode { get; set; }
    public string ProductCode { get; set; }
    public List<CardDto> Cards { get; set; }
    public string ErrorDetail { get; set; }
}