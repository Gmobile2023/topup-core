using System;
using Topup.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace Topup.Report.Domain.Entities;

public class ReportBalanceHistories : Document
{
    public double Amount { get; set; }
    public string CurrencyCode { get; set; }
    public string SrcAccountCode { get; set; }
    public string DesAccountCode { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public double SrcAccountBalanceAfterTrans { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public double DesAccountBalanceAfterTrans { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public double SrcAccountBalanceBeforeTrans { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public double DesAccountBalanceBeforeTrans { get; set; }

    public string TransRef { get; set; }
    public string TransCode { get; set; }

    [BsonRepresentation(BsonType.Int32)] public byte Status { get; set; }

    public string ModifiedBy { get; set; }
    public string CreatedBy { get; set; }
    public string RevertTransCode { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; }
    public string TransNote { get; set; }
    public string TransactionType { get; set; }

    [BsonRepresentation(BsonType.Int32)] public TransactionType TransType { get; set; }

    public string MobileNumber { get; set; }

    public string PartnerCode { get; set; }
    public string Vendor { get; set; }

    public string SrcAccountType { get; set; }
    public string DesAccountType { get; set; }
    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }
}