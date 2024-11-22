using System;
using System.Collections.Generic;
using Topup.Report.Model.Dtos;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace Topup.Report.Domain.Entities;

public class ReportAccountBalanceDay : Document
{
    public double? LimitBefore { get; set; }
    public double BalanceBefore { get; set; }
    public double Debit { get; set; }
    public double Credite { get; set; }
    public double? IncDeposit { get; set; }
    public double? DecPayment { get; set; }
    public double? DecOther { get; set; }
    public double? IncOther { get; set; }
    public double BalanceAfter { get; set; }
    public double? LimitAfter { get; set; }
    public DateTime CreatedDay { get; set; }
    public string AccountType { get; set; }
    public string CurrencyCode { get; set; }
    public string AccountCode { get; set; }
    public string AccountInfo { get; set; }
    public string SaleCode { get; set; }
    public string SaleLeaderCode { get; set; }
    public string ParentCode { get; set; }
    public int? AgentType { get; set; }
    public string TextDay { get; set; }
}

public class ReportAccountBalanceDayTemp : ReportAccountBalanceDayInfo
{
    public DateTime MaxDate { get; set; }
    public DateTime MinDate { get; set; }
    public DateTime CreatedDay { get; set; }
}

public class ReportAccountBalanceDayInfo
{
    public string AccountCode { get; set; }
    public string AccountInfo { get; set; }
    public int AgentType { get; set; }

    public double BalanceBefore { get; set; }

    public double Debit { get; set; }

    public double Credited { get; set; }

    public double BalanceAfter { get; set; }
}

public class ReportCheckBalanceText : ReportAccountBalanceDayInfo
{
    public string CurrencyCode { get; set; }

    public string TextDay { get; set; }
}

public class ReportCheckBalance
{
    public List<ReportCheckBalanceText> Historys { get; set; }
    public List<ReportCheckBalanceText> Balances { get; set; }
    public List<ReportCheckBalanceText> UpdateBalances { get; set; }
    public List<ReportCheckBalanceText> BalanceOtherHistory { get; set; }
}

public class ReportSendMailAgentApi
{
    public string AgentInfo { get; set; }
    public string Description { get; set; }
    public bool IsSend { get; set; }
}