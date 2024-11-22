using Topup.Shared;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;

namespace Topup.Gw.Model.Commands;

public interface TransactionRefundCommand : ICommand
{
    string TransCode { get; set; }
}

public class CallBackTransCommand : ICommand
{
    public string AccountCode { get; set; }
    public string TransCode { get; set; }
    public string ProviderCode { get; set; }
    public decimal Amount { get; set; }
    public int Status { get; set; }
    public bool? IsRefund { get; set; }
    public Guid CorrelationId { get; set; }
}

public class TransGatePushCommand : ICommand
{
    public string TransCode { get; set; }    
    public decimal FirstAmount { get; set; }
    public decimal TransAmount { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Provider { get; set; }
    public string FirstProvider { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
    public string Mobile { get; set; }
    public string Vender { get; set; }

    [BsonRepresentation(BsonType.Int32)]
    public SaleRequestStatus Status { get; set; }

    public string ChartId { get; set; }
    public string Type { get; set; }
    public Guid CorrelationId { get; set; }
}