using System;
using GMB.Topup.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace GMB.Topup.Gw.Domain.Entities;

public class CardBatchRequest : Document
{
    public int Quantity { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal Amount { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal Price { get; set; }

    public decimal? DiscountRate { get; set; }
    public decimal? DiscountAmount { get; set; }

    [BsonRepresentation(BsonType.Int32)] public CardBatchRequestStatus Status { get; set; }

    public DateTime CreatedTime { get; set; }
    public DateTime RequestDate { get; set; }
    public DateTime? EndDate { get; set; }

    [BsonRepresentation(BsonType.Int32)] public Channel Channel { get; set; }

    public string TransCode { get; set; }
    public string Provider { get; set; }

    public string Vendor { get; set; }
    public string ServiceCode { get; set; }
    public string ProductCode { get; set; }
    public string CategoryCode { get; set; }
    public string ExtraInfo { get; set; }
    public string ProviderTransCode { get; set; }
    public string RequestIp { get; set; }

    public string UserProcess { get; set; }
}