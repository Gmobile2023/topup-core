using System;
using HLS.Paygate.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Commission.Domain.Entities;

public class CommissionTransaction : Document
{
    public string ReceiverInfo { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal CommissionAmount { get; set; }

    [BsonRepresentation(BsonType.Int32)] public CommissionTransactionStatus Status { get; set; }

    [BsonRepresentation(BsonType.Int32)] public SaleType SaleType { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    [BsonRepresentation(BsonType.Int32)] public Channel Channel { get; set; }
    public string PartnerCode { get; set; }
    public string ParentCode { get; set; }
    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public string ServiceCode { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal PaymentAmount { get; set; }

    public string ProductCode { get; set; } //Sản phẩm
    public string CategoryCode { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ParentDiscountAmount { get; set; }
    public decimal Amount { get; set; }
    public int Quantity { get; set; }
}