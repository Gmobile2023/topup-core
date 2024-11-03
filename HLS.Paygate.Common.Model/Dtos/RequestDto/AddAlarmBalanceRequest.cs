using ServiceStack;
using HLS.Paygate.Shared;
namespace HLS.Paygate.Common.Model.Dtos.RequestDto;

[Route("/api/v1/common/alarm/balance/add", "POST")]
public class AddAlarmBalanceRequest : IPost
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