using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace Topup.Common.Domain.Entities;

public class AlarmBalanceConfig : Document
{
    public int? TenantId { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
    public DateTime CreatedDate { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
    public DateTime? ModifiedDate { get; set; }
    public string Channel { get; set; }
    public string AccountCode { get; set; }
    public string AccountName { get; set; }
    public decimal MinBalance { get; set; }
    public long TeleChatId { get; set; }
    public string CurrencyCode { get; set; }
    public bool IsRun { get; set; }
}