using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace GMB.Topup.Gw.Domain.Entities;

public class AccountTransLimitConfig : Document
{
    //[BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    //public decimal LimitAmount { get; set; }
    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal LimitPerDay { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal? LimitPerTrans { get; set; }

    public string ServiceCode { get; set; }
    public string CateroryCode { get; set; }
    public string ProductCode { get; set; }
    public string AccountCode { get; set; }
}