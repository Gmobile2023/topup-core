using Topup.Shared;
using ServiceStack;

namespace Topup.Balance.Models.Requests;

[Route("/api/v1/partner/balance/{PartnerCode}", "GET")]
public class PartnerBalanceGetRequest : IGet, IReturn<object>
{
    public string PartnerCode { get; set; }
}