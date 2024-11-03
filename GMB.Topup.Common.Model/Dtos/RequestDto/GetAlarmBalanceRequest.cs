using ServiceStack;
namespace GMB.Topup.Common.Model.Dtos.RequestDto;

[Route("/api/v1/common/alarm/balance", "GET")]
public class GetAlarmBalanceRequest : IGet
{
    public int? TenantId { get; set; }
    public string AccountCode { get; set; }
    public string CurrencyCode { get; set; }
}