using System;
using HLS.Paygate.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Common.Domain.Entities;

public class PayBillAccounts : Document
{
    public int? TenantId { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
    public DateTime CreatedDate { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
    public DateTime? ModifiedDate { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
    public DateTime? LastQueryDate { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
    public DateTime? LastTransDate { get; set; }

    public string Description { get; set; }
    public string AccountCode { get; set; }
    public string ProductCode { get; set; }
    public string CategoryCode { get; set; }
    public string ServiceCode { get; set; }
    public string ProductName { get; set; }
    public string LastProviderCode { get; set; }
    public string LastTransCode { get; set; }
    public string InvoiceInfo { get; set; }
    public string InvoiceQueryInfo { get; set; }
    public decimal PaymentAmount { get; set; }
    public string InvoiceCode { get; set; }
    public string ExtraInfo { get; set; }

    [BsonRepresentation(BsonType.Int32)] public PayBillCustomerStatus Status { get; set; }

    public bool IsQueryBill { get; set; }
    public bool IsLastSuccess { get; set; }
    public bool IsInvoice { get; set; }
    public int RetryCount { get; set; }
    public string ResponseQuery { get; set; }
}