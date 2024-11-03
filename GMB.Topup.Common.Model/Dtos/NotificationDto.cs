using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GMB.Topup.Common.Model.Dtos;

public class PushNotificationDto
{
    public string to { get; set; }
    public NotificationMessageDto notification { get; set; }
    public string priority { get; set; }
    public object data { get; set; }
    public List<string> registration_ids { get; set; }
}

public class NotificationMessageDto
{
    public string title { get; set; }
    public string body { get; set; }
    public string sound { get; set; }
    public string click_action { get; set; }
}

public class NotificationAppData
{
    [DataMember(Name = "Type")] public string Type { get; set; }

    [DataMember(Name = "Properties")] public object Properties { get; set; }
}

public static class NotificationType
{
    public const string Notification = "Notification";
}

public class ScreenActionDto
{
    public string Screen { get; set; }
    public ParamsScreenDto Params { get; set; }
}

public class ParamsScreenDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
}

public class CreateNotifiOutDto
{
    public Guid NotificationId { get; set; }
    public string NotifiTypeCode { get; set; }
}

public enum UserNotificationState
{
    Unread = 0,
    Read = 1
}

public static class NotifiAppDeivceType
{
    public const string IOS = "IOS";
    public const string Android = "Android";
    public const string Web = "Web";
}

public enum NotificationSeverity : byte
{
    /// <summary>Info.</summary>
    Info,

    /// <summary>Success.</summary>
    Success,

    /// <summary>Warn.</summary>
    Warn,

    /// <summary>Error.</summary>
    Error,

    /// <summary>Fatal.</summary>
    Fatal
}

public class FcmResultDto
{
    public long multicast_id { get; set; }
    public int success { get; set; }
    public int failure { get; set; }
    public int canonical_ids { get; set; }
    public List<FcmMessageIdDto> results { get; set; }
}

public class FcmMessageIdDto
{
    public string message_id { get; set; }
}