using System;
using GMB.Topup.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace GMB.Topup.Common.Domain.Entities;

public class AuditAccountActivities : Document
{
    public string AccountCode { get; set; }
    public int? TenantId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string FullName { get; set; }
    public int AccountType { get; set; }
    public int AgentType { get; set; }
    public string PhoneNumber { get; set; }
    public string UserCreated { get; set; }
    public string UserModifed { get; set; }
    public string Note { get; set; }
    public string Payload { get; set; }
    public string SrcValue { get; set; }
    public string DesValue { get; set; }

    [BsonRepresentation(BsonType.Int32)] public AccountActivityType AccountActivityType { get; set; }

    public string Attachment { get; set; }
}