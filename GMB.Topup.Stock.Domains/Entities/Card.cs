using System;
using GMB.Topup.Shared.Utils;
using GMB.Topup.Stock.Contracts.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace GMB.Topup.Stock.Domains.Entities;

public class Card : Document
{
    public string Serial { get; set; }
    public string CardCode { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal CardValue { get; set; }

    [BsonRepresentation(BsonType.Int32)] public CardStatus Status { get; set; }

    public DateTime ImportedDate { get; set; }
    public DateTime ExpiredDate { get; set; }
    public DateTime? ExportedDate { get; set; }
    public string BatchCode { get; set; }
    public string StockCode { get; set; }
    public string ProductCode { get; set; }

    [BsonRepresentation(BsonType.String, AllowTruncation = true)]
    public string Hashed => (Serial + CardCode).Md5(); //{ get; set; }

    public string ProviderCode { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string TransCode { get; set; }

    // [BsonRepresentation(BsonType.Int32)]
    // public CardBatchType BatchType { get; set; }
}