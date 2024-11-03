using System;

namespace HLS.Paygate.Gw.Model.Dtos;

public class SaleHistoryDto : SaleRequestDto
{
    public string TopupTransCode { get; set; }
    public string CardTransCode { get; set; }
    public string CardCode { get; set; }
    public string Serial { get; set; }
    public int CardValue { get; set; }
    public decimal ItemAmount { get; set; }
    public decimal TopupItemAmount { get; set; }
    public DateTime ExpiredDate { get; set; }
    public string TopupTransactionType { get; set; }
}