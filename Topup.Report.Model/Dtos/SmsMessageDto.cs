using System;
using Topup.Shared;

namespace Topup.Report.Model.Dtos;

public class SmsMessageDto
{
    public int? TenantId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string Description { get; set; }
    public string AccountCode { get; set; }
    public string PhoneNumber { get; set; }
    public string SmsChannel { get; set; }
    public Channel Channel { get; set; }
    public string Result { get; set; }
    public int Status { get; set; }
    public string Message { get; set; }
}