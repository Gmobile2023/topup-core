using ServiceStack;
using Topup.Shared;

namespace Topup.Common.Model.Dtos.RequestDto;

[Route("/api/v1/common/alarm/balance/update", "PUT")]
public class UpdateAlarmBalanceRequest:IPut
{
    public int? TenantId { get; set; }
    public string Channel { get; set; }
    public string AccountCode { get; set; }
    public string AccountName { get; set; }
    public decimal MinBalance { get; set; }
    public long TeleChatId { get; set; }
    public string CurrencyCode { get; set; }
    public bool IsRun { get; set; }
}