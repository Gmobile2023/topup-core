using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Report.Domain.Entities
{
    public class SimBalanceHistories : Document
    {
        public string SimNumber { get; set; }
        [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
        public decimal BalanceBefore { get; set; }
        [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
        public decimal BalanceAfter { get; set; }
        [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
        public decimal Increase { get; set; }
        [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
        public decimal Decrease { get; set; }
        public string TransRef { get; set; }
        public string TransCode { get; set; }
        [BsonRepresentation(BsonType.Int32)] public byte Status { get; set; }
        public string ModifiedBy { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string Serial { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Description { get; set; }
        public string TransNote { get; set; }
        public string TransactionType { get; set; }
    }
}