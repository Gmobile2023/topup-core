using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.Balance.Models.Requests;

[Route("/api/v1/balance/check-balance", "GET")]
public class AccountBalanceGetRequest : IGet, IReturn<object>
{
    public string PartnerCode { get; set; }
}