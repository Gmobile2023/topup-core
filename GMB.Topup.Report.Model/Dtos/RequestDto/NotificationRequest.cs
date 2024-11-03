using System;
using GMB.Topup.Report.Model.Dtos.ResponseDto;
using GMB.Topup.Shared;
using ServiceStack;

namespace GMB.Topup.Report.Model.Dtos.RequestDto;

[Route("/api/v1/report/notification/getall", "GET")]
public class GetUserNotificationRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string NotificationType { get; set; }
    public int? State { get; set; }
    public string AccountCode { get; set; }
    public bool? IsTotalOnly { get; set; }
}

[Route("/api/v1/report/notification/get_last", "GET")]
public class GetLastNotificationRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string NotificationType { get; set; }
    public int? State { get; set; }
    public string AccountCode { get; set; }
    public bool? IsTotalOnly { get; set; }
}

[Route("/api/v1/report/notification/set_all_as_read", "POST")]
public class SetAllNotificationsAsReadRequest
{
    public string AccountCode { get; set; }
}

[Route("/api/v1/report/notification/set_as_read", "POST")]
public class SetNotificationAsReadRequest
{
    public Guid Id { get; set; }
    public string AccountCode { get; set; }
}

[Route("/api/v1/report/notification/delete", "DELETE")]
public class DeleteNotificationRequest
{
    public string AccountCode { get; set; }
    public Guid Id { get; set; }
}

[Route("/api/v1/report/notification/subscribe", "POST")]
public class SubscribeNotificationRequest
{
    public string AccountCode { get; set; }
    public int? TenantId { get; set; }

    /// <summary>
    ///     Thông tin version và hệ điều hành. Tuyền vào theo định dạng IOS-xxx, Android-xxx. xxx là version của app.
    /// </summary>
    public string AppVersion { get; set; }

    /// <summary>
    ///     Token
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    ///     Kích thước màn hình sử dụng width-height
    /// </summary>
    public string ScreenSize { get; set; }

    /// <summary>
    ///     Vị trí ng dùng app: truyền dạng Latitude-Longitude
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    ///     Tên thiết bị sử dụng
    /// </summary>
    public string DeviceName { get; set; }

    public string DeviceType { get; set; }

    /// <summary>
    ///     Phiên bản hệ điều hành
    /// </summary>
    public string DeviceVersion { get; set; }

    /// <summary>
    ///     Kênh WEB hoặc APP
    /// </summary>
    public Channel Channel { get; set; }
}

[Route("/api/v1/report/notification/un_subscribe", "POST")]
public class UnSubscribeNotificationRequest
{
    public string AccountCode { get; set; }
    public string Token { get; set; }
}

[Route("/api/v1/report/notification/un_subscribe_account", "POST")]
public class UnSubscribeAcountNotificationRequest
{
    public string Token { get; set; }
    public string AccountCode { get; set; }
}

[Route("/api/v1/report/notification/get", "GET")]
public class GetNotificationRequest : IReturn<NotificationAppOutDto>
{
    public string AccountCode { get; set; }
    public Guid Id { get; set; }
}

[Route("/api/v1/report/notification/send", "POST")]
public class SendNotificationRequest
{
    public string Title { get; set; }
    public string Body { get; set; }
    public string Data { get; set; }
    public Guid NotificationId { get; set; }
    public string NotifiTypeCode { get; set; }
    public string Icon { get; set; } = "logo.png";
    public string IconNotifi { get; set; }
    public string Image { get; set; }
    public string Tag { get; set; }

    public string Slug { get; set; }

    //public ScreenActionDto ScreenAction { get; set; }
    public string Severity { get; set; }
    public int TotalUnRead { get; set; }
    public int? TenanId { get; set; }
    public string Type { get; set; } = NotificationType.Notification;
    public string AppNotificationName { get; set; }
    public string Link { get; set; }
    public string AccountCode { get; set; }
}