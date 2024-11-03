using System;
using System.Collections.Generic;

namespace HLS.Paygate.Common.Model.Dtos.ResponseDto;

public class NotificationResponseDto
{
}

public class ShowNotificationDto
{
    public List<NotificationAppOutDto> LastNotification { get; set; }
    public int Total { get; set; }
    public int TotalUnRead { get; set; }
    public int TotalRead { get; set; }
}

public class NotificationAppOutDto
{
    public int? TenantId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string Description { get; set; }
    public string Body { get; set; }
    public string Data { get; set; }
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
    public Guid Id { get; set; }
    public string AppNotificationName { get; set; }
}