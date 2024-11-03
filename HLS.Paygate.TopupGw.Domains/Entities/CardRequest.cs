using System;
using HLS.Paygate.Gw.Model.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Stock.Domains.Entities
{
    public class CardRequest : Document
    {
        public string Serial { get; set; }
        public string CardCode { get; set; }

        [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
        public int RequestValue { get; set; }

        [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
        public int RealValue { get; set; }

        [BsonRepresentation(BsonType.Int32)] 
        public CardRequestStatus Status { get; set; }
        public string Vendor { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? ExpiredDate { get; set; }
        public DateTime? ExportedDate { get; set; }
        public DateTime StartProcessTime { get; set; }
        public DateTime EndProcessTime { get; set; }
        public string ProviderCode { get; set; }
        public string SupplierCode { get; set; }
        public string TransRef { get; set; }
        public string TransCode { get; set; }
        // public bool ProcessPartner { get; set; } //Giao dịch gọi sang kênh đối tác
        // public string PartnerTransCode { get; set; } //Mã giao dịch khi gọi sang kênh đối tác
        // public string CallBackUrl { get; set; }
        // public int WaitingTimeInSeconds { get; set; }
        // public string RefStatus { get; set; }
    }
}