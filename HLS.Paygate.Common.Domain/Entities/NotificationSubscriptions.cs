using System;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Common.Domain.Entities;

public class NotificationSubscriptions : Document
{
    public string AccountCode { get; set; }
    public int? TenantId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string Description { get; set; }
    public string Token { get; set; }
    public string DeviceName { get; set; }
    public string DeviceVersion { get; set; }
    public string AppVersion { get; set; }
    public string AppType { get; set; }
    public long? UserId { get; set; }
    public string ScreenSize { get; set; }
    public string Location { get; set; }
    public string Channel { get; set; }
    public bool IsReceive { get; set; }
}