using System;
using HLS.Paygate.Balance.Models.Enums;
using HLS.Paygate.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Balance.Domain.Entities;

public class Transaction : Document
{
    public string TransactionCode { get; set; }
    public string SrcAccountCode { get; set; }
    public string DesAccountCode { get; set; }
    public string TransRef { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; }

    [BsonRepresentation(BsonType.Int32)] public TransactionType TransType { get; set; }

    [BsonRepresentation(BsonType.Int32)] public TransStatus Status { get; set; }

    public DateTime? ModifiedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string ModifiedBy { get; set; }
    public string CreatedBy { get; set; }
    public string Description { get; set; }
    public string RevertTransCode { get; set; }
    public string TransNote { get; set; }
}