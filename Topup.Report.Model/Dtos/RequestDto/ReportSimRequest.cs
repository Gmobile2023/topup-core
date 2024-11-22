using System;
using System.Collections.Generic;
using Topup.Shared;
using ServiceStack;

namespace Topup.Report.Model.Dtos.RequestDto;

[Route("/api/v1/report/sim/balance_histories", "GET")]
public class SimBalanceHistoriesRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string SimNumber { get; set; }
    public string TransCode { get; set; }
    public string TransRef { get; set; }
    public string Serial { get; set; }
    public string CardCode { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

[Route("/api/v1/report/sim/balance", "GET")]
public class SimBalanceDateRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string SimNumber { get; set; }
    public string TransCode { get; set; }
    public string TransRef { get; set; }
    public string Serial { get; set; }
    public string CardCode { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

[Route("/api/v1/report/sim/check-mobile-trans", "GET")]
public class CheckMobileTransRequest
{
    public string Mobile { get; set; }
    public string Provider { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

[Route("/api/v1/report/remove-cache", "POST")]
public class RemoveKeyRequest
{
    public string Key { get; set; }
}