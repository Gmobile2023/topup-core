using System.Collections.Generic;
using HLS.Paygate.Shared.ConfigDtos;

namespace HLS.Paygate.Common.Model.Dtos;

public class AutoCheckMinBalance
{
    public bool IsRun { get; set; }
    public int TimeRun { get; set; }
    public string CronExpression { get; set; }
    public List<AccountCheckInfo> AccountsCheck { get; set; }
    public decimal MinBalance { get; set; }
    public int ChatId { get; set; }
}

public class AccountCheckInfo
{
    public string AccountCode { get; set; }
    public string AccountName { get; set; }
    public string CurrencyCode { get; set; }
}

public class SendMailStockMinInventoryDto
{
    public bool IsSendMail { get; set; }
    public bool IsBotMessage { get; set; }
    public int TimeReSend { get; set; }
    public int SendCount { get; set; }
    public string EmailReceive { get; set; }
}

public class CommonHangFireConfig: HangFireConfig
{
    public AutoQueryBill AutoQueryBill { get; set; }
    public AutoCheckMinBalance AutoCheckMinBalance { get; set; }
}
public class AutoQueryBill
{

    public bool IsTest { get; set; }
    public bool IsRun { get; set; }
    public int TimeRun { get; set; }
    public string CronExpression { get; set; }
    public string CronExpressionTest { get; set; }
    public int TimeRunTest { get; set; }
    public int RetryCount { get; set; }
}
