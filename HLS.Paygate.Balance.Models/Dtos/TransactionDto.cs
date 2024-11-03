using System;
using System.Collections.Generic;
using HLS.Paygate.Balance.Models.Enums;
using HLS.Paygate.Shared;
using Orleans;

namespace HLS.Paygate.Balance.Models.Dtos;

public class TransactionDto
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
    public TransStatus Status { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string ModifiedBy { get; set; }
    public string CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; }
    public string TransNote { get; set; }
    public string RevertTransCode { get; set; }
    public List<SettlementDto> Settlements { get; set; }
}

[GenerateSerializer]
public class SettlementDto
{
    [Id(1)]
    public Guid Id { get; set; }
    [Id(2)]
    public DateTime AddedAtUtc { get; set; }
    [Id(3)]
    public decimal Amount { get; set; }
    [Id(4)]
    public string CurrencyCode { get; set; }
    [Id(5)]
    public string SrcAccountCode { get; set; }
    [Id(6)]
    public string SrcShardAccountCode { get; set; }
    [Id(7)]
    public string DesAccountCode { get; set; }
    [Id(8)]
    public string DesShardAccountCode { get; set; }
    [Id(9)]
    public decimal SrcAccountBalance { get; set; }
    [Id(10)]
    public decimal DesAccountBalance { get; set; }
    [Id(11)]
    public decimal SrcAccountBalanceBeforeTrans { get; set; }
    [Id(12)]
    public decimal DesAccountBalanceBeforeTrans { get; set; }
    [Id(13)]
    public string TransRef { get; set; }
    [Id(14)]
    public string TransCode { get; set; }
    [Id(15)]
    public SettlementStatus Status { get; set; }
    [Id(16)]
    public DateTime? ModifiedDate { get; set; }
    [Id(17)]
    public DateTime? CreatedDate { get; set; }
    [Id(18)]
    public string TransactionType { get; set; }
    [Id(19)]
    public string Description { get; set; }
    [Id(20)]
    public bool ReturnResult { get; set; }
    [Id(21)]
    public string PaymentTransCode { get; set; }//Mã gọi sang từ đối tác
    [Id(22)]
    public TransactionType TransType { get; set; }
}