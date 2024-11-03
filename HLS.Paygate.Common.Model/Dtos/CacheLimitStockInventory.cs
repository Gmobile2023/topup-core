using System;

namespace HLS.Paygate.Common.Model.Dtos;

public class CacheSendEmailLimitMinStockInventoryDto
{
    public int TotalSend { get; set; }
    public DateTime LastTimeSend { get; set; }
}