using System;
using GMB.Topup.Balance.Models.Enums;
using GMB.Topup.Shared;

namespace GMB.Topup.Balance.Models.Dtos;

public class TransactionReportDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; }
    public string SrcAccountCode { get; set; }
    public string DesAccountCode { get; set; }
    public decimal SrcAccountBalance { get; set; }
    public decimal DesAccountBalance { get; set; }
    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public TransStatus Status { get; set; }
    public string ModifiedBy { get; set; }
    public string CreatedBy { get; set; }
    public string RevertTransCode { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; }
    public string TransNote { get; set; }
    public string TransactionType { get; set; }
    public TransactionType TransType { get; set; }
}

public class BalanceHistoryDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; }
    public string SrcAccountCode { get; set; }
    public string DesAccountCode { get; set; }
    public decimal SrcAccountBalance { get; set; }
    public decimal DesAccountBalance { get; set; }
    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public TransStatus Status { get; set; }
    public string ModifiedBy { get; set; }
    public string CreatedBy { get; set; }
    public string RevertTransCode { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; }
    public string TransNote { get; set; }
    public string TransactionType { get; set; }
    public TransactionType TransType { get; set; }
}