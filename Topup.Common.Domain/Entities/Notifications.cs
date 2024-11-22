using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace Topup.Common.Domain.Entities;

public class Notifications : Document
{
    public int? TenantId { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
    public DateTime CreatedDate { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
    public DateTime? ModifiedDate { get; set; }

    public string Description { get; set; }
    public string NotificationType { get; set; }
    public string NotificationName { get; set; }
    public string AccountCode { get; set; }
    public string AppNotificationName { get; set; }
}