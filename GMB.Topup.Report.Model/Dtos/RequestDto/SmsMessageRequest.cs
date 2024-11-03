using ServiceStack;

namespace GMB.Topup.Report.Model.Dtos.RequestDto;

[Route("/api/v1/report/sms/add", "POST")]
public class SmsMessageRequest
{
    public int? TenantId { get; set; }
    public string Description { get; set; }
    public string AccountCode { get; set; }
    public string PhoneNumber { get; set; }
    public string Channel { get; set; }
    public string Result { get; set; }
    public int Status { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }
    public string TramsCode { get; set; }
    public string SmsChannel { get; set; }
}