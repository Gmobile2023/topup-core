using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Gw.Domain.Entities;

public class Invoices : Document
{
    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal Amount { get; set; }

    public DateTime CreatedTime { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public string CustomerReference { get; set; }
    public string Address { get; set; }
    public string Period { get; set; }
    public string PhoneNumber { get; set; }
    public string TransCode { get; set; }
    public string TransRef { get; set; }
    public string Description { get; set; }
    public string ExtraInfo { get; set; }
}