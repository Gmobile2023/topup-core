using System;
using GMB.Topup.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;
namespace GMB.Topup.Gw.Domain.Entities
{
    public class SaleOffsetRequest : Document
    {
        public string ReceiverInfo { get; set; }

        [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
        public decimal Amount { get; set; }     

        [BsonRepresentation(BsonType.Int32)]
        public SaleRequestStatus Status { get; set; }      
        public DateTime CreatedTime { get; set; }

        public DateTime OriginCreatedTime { get; set; }
        public string OriginPartnerCode { get; set; }      
        public string OriginTransRef { get; set; }
        public string OriginTransCode { get; set; }
        public string OriginProviderCode { get; set; }
        public string ProductCode { get; set; }
        public string PartnerCode { get; set; }
        public string TransRef { get; set; }
        public string TransCode { get; set; }        
        public string Vendor { get; set; }      
        public string ServiceCode { get; set; }       
        
        public string ProviderCode { get; set; }
    }
}
