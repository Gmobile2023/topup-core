using System;
using System.Collections.Generic;
using Topup.Shared;

namespace Topup.Gw.Model.Events;

public interface ReportBalanceHistoriesMessage : IEvent
{
    public TransactionReportDto Transaction { get; }
    public SettlementReportDto Settlement { get; set; }
    public string ExtraInfo { get; set; }
}

public class SettlementReportDto
{
    public Guid Id { get; set; }
    public DateTime AddedAtUtc { get; set; }
    public double Amount { get; set; }
    public string CurrencyCode { get; set; }
    public string SrcAccountCode { get; set; }
    public string DesAccountCode { get; set; }
    public double SrcAccountBalance { get; set; }
    public double DesAccountBalance { get; set; }
    public double SrcAccountBalanceBeforeTrans { get; set; }
    public double DesAccountBalanceBeforeTrans { get; set; }
    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string TransactionType { get; set; }
    public string Description { get; set; }
}

public class TransactionReportDto
{
    public Guid Id { get; set; }
    public DateTime AddedAtUtc { get; set; }
    public string TransactionCode { get; set; }
    public string SrcAccountCode { get; set; }
    public string DesAccountCode { get; set; }
    public string TransRef { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public string CurrencyCode { get; set; }
    public TransactionType TransType { get; set; }
    public byte Status { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string ModifiedBy { get; set; }
    public string CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; }
    public string TransNote { get; set; }
    public string RevertTransCode { get; set; }
    public List<SettlementReportDto> Settlements { get; set; }
}