using System;
using Topup.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace Topup.Gw.Domain.Entities
{
    public class SaleGateRequest : Document
    {
        public string TransCode { get; set; }

        [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
        public decimal TransAmount { get; set; }

        [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
        public decimal FirstAmount { get; set; }

        [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
        public decimal TopupAmount { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Provider { get; set; }
        public string FirstProvider { get; set; }
        public string TopupProvider { get; set; }
        public string ServiceCode { get; set; }
        public string CategoryCode { get; set; }
        public string ProductCode { get; set; }

        [BsonRepresentation(BsonType.Int32)]
        public SaleRequestStatus Status { get; set; }
        public string Mobile { get; set; }
        public string Vender { get; set; }
        public string Type { get; set; }
        public string ChartId { get; set; }

    }
}
