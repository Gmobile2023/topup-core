﻿using ServiceStack;
using GMB.Topup.Shared;

namespace GMB.Topup.Common.Model.Dtos.RequestDto;

[Route("/api/v1/common/alarm/balance/get-all", "GET")]
public class GetAllAlarmBalanceRequest : PaggingBase, IGet
{
    public int? TenantId { get; set; }
    public string AccountCode { get; set; }
    public string CurrencyCode { get; set; }
}