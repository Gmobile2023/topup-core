using System;
using Topup.Balance.Models.Enums;
using Topup.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace Topup.Balance.Domain.Entities;

public class TransactionReport : Document
{
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; }
    public string SrcAccountCode { get; set; }
    public string DesAccountCode { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal SrcAccountBalance { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal DesAccountBalance { get; set; }

    public string TransRef { get; set; }
    public string TransCode { get; set; }

    [BsonRepresentation(BsonType.Int32)] public TransStatus Status { get; set; }

    public string ModifiedBy { get; set; }
    public string CreatedBy { get; set; }
    public string RevertTransCode { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; }
    public string TransNote { get; set; }
    public string TransactionType { get; set; }

    [BsonRepresentation(BsonType.Int32)] public TransactionType TransType { get; set; }
}