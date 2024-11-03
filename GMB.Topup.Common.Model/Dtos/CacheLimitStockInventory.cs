using System;

namespace GMB.Topup.Common.Model.Dtos;

public class CacheSendEmailLimitMinStockInventoryDto
{
    public int TotalSend { get; set; }
    public DateTime LastTimeSend { get; set; }
}