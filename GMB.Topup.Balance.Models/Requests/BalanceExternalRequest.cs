using GMB.Topup.Shared;
using ServiceStack;

namespace GMB.Topup.Balance.Models.Requests;

[Route("/api/v1/balance/check-balance", "GET")]
public class AccountBalanceGetRequest : IGet, IReturn<object>
{
    public string PartnerCode { get; set; }
}