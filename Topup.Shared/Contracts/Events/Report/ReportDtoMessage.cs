﻿using System;

namespace Topup.Shared.Contracts.Events.Report;
public class ReportSaleMessage : IEvent
{
    public string RequestCode { get; set; }
    public string TransCode { get; set; }
    public string PaymentCode { get; set; }
    public string PayTransCode { get; set; }
    public string ProviderCode { get; set; }
    public string ProcessAccount { get; set; }
    public string AccountCode { get; set; }
    public string ParentCode { get; set; }
    public decimal Amount { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal Fee { get; set; }
    public string ServiceCode { get; set; }
    public string ProductCode { get; set; }
    public string ReceivedAccount { get; set; }
    public DateTime CreatedDate { get; set; }
    public decimal Balance { get; set; }  
    public int Status { get; set; }
    public int NextStep { get; set; }
    public string ExtraInfo { get; set; }
    public string Channel { get; set; }
    public string FeeInfo { get; set; }
    public string ReceiverType { get; set; }
    public string CurrencyCode { get; set; }
    public int Retry { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid EventId { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ReportDepositMessage
{
    public Guid Id { get; set; }
    public string RequestCode { get; set; }
    public string TransCode { get; set; }
    public string AccountCode { get; set; }
    public double Amount { get; set; }
    public double Price { get; set; }
    public string ServiceCode { get; set; }
    public DateTime CreatedDate { get; set; }
    public double Balance { get; set; }
    public string SaleProcess { get; set; }     
    public string CurrencyCode { get; set; }
    public string TransNote { get; set; }
    public string Description { get; set; }
    public string ExtraInfo { get; set; }    
}

public class ReportTransferMessage
{
    public Guid Id { get; set; }
    public string RequestCode { get; set; }
    public string TransCode { get; set; }
    public string AccountCode { get; set; }
    public double Amount { get; set; }
    public double Price { get; set; }
    public string ServiceCode { get; set; }
    public string ReceivedAccountCode { get; set; }
    public DateTime CreatedDate { get; set; }
    public double Balance { get; set; }
    public double ReceivedBalance { get; set; }
    public string CurrencyCode { get; set; }
    public string TransNote { get; set; }
    public string Description { get; set; }
}

public class ReportTransStatusMessage
{
    public string TransCode { get; set; }
    public string PayTransCode { get; set; }
    public string ProviderCode { get; set; }     
    
    public int SaleType { get; set; }
    public int Status { get; set; }
}

public class ReportCompensationHistoryMessage
{
    public string PaymentCode { get; set; }
    public string ServiceCode { get; set; }
}

public class ReportSyncAccounMessage
{
    public long UserId { get; set; }
    public string AccountCode { get; set; }
}

public class ReportCommistionMessage
{
    public int Type { get; set; }
    public string ParentCode { get; set; }
    public string TransCode { get; set; }
    public decimal CommissionAmount { get; set; }
    public DateTime? CommissionDate { get; set; }
    public int Status { get; set; }
    public string CommissionCode { get; set; }
}

public class ReportRefundMessage
{
    public string TransCode { get; set; }
    public string PaymentCode { get; set; }
    public int Retry { get; set; }
}

public class ReportSaleRequestMessage : IEvent
{
    public string TransCode { get; set; }
    public int Status { get; set; }
    public int NextStep { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid EventId { get; set; }
    public DateTime Timestamp { get; set; }
}