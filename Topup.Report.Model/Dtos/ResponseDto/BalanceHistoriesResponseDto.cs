using System;
using Topup.Shared;

namespace Topup.Report.Model.Dtos.ResponseDto;

public class BalanceHistoriesResponseDto
{
    public Guid Id { get; set; }
    public double Amount { get; set; }
    public string CurrencyCode { get; set; }
    public string SrcAccountCode { get; set; }
    public string DesAccountCode { get; set; }
    public double SrcAccountBalance { get; set; }
    public double DesAccountBalance { get; set; }
    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public byte Status { get; set; }
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