using System.Collections.Generic;
using HLS.Paygate.Stock.Contracts.Dtos;

namespace HLS.Paygate.Stock.Contracts.Events;

public record CardExportRollbackEvent
{
    public string StockCode { get; set; }
    public string ProductCode { get; set; }
    public List<CardDto> Cards { get; set; }
    public string ErrorDetail { get; set; }
}