using System;
using MongoDbGenericRepository.Models;

namespace GMB.Topup.Common.Domain.Entities;

public class NotificationMesssage : Document
{
    public string AccountCode { get; set; }
    public int? TenantId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string Description { get; set; }
    public string Body { get; set; }
    public string Data { get; set; }

    /// <summary>
    ///     ID thông báo
    /// </summary>
    public Guid NotifitionId { get; set; }

    public string Title { get; set; }
    public long? UserId { get; set; }
    public string Icon { get; set; }

    /// <summary>
    ///     Trạng thái đã đọc chưa
    /// </summary>
    public byte State { get; set; }

    public long? CreatorUserId { get; set; }
    public string NotificationType { get; set; }
    public string AppNotificationName { get; set; }
    public string Severity { get; set; }
}