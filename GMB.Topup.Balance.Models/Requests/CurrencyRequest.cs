using GMB.Topup.Shared;
using ServiceStack;

namespace GMB.Topup.Balance.Models.Requests;

[Route("/api/balance/currency", "GET")]
[Route("/api/balance/currency/{CurrencyCode}", "GET")]
public class CheckCurrencyRequest : IGet, IReturn<MessageResponseBase>
{
    public string CurrencyCode { get; set; }
}