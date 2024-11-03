using System;
using HLS.Paygate.Balance.Models.Enums;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Messages.Events;

namespace HLS.Paygate.Balance.Models.Events;

public interface TransactionReportCreated : IEvent
{
    decimal Amount { get; set; }
    string CurrencyCode { get; set; }
    string SrcAccountCode { get; set; }
    string DesAccountCode { get; set; }
    decimal SrcAccountBalance { get; set; }
    decimal DesAccountBalance { get; set; }
    string TransRef { get; set; }
    string TransCode { get; set; }
    TransStatus Status { get; set; }
    TransactionType TransType { get; set; }
    string ModifiedBy { get; set; }
    string CreatedBy { get; set; }
    string RevertTransCode { get; set; }
    DateTime? ModifiedDate { get; set; }
    DateTime? CreatedDate { get; set; }
    string Description { get; set; }
    string TransNote { get; set; }
    string TransactionType { get; set; }
}