using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.Balance.Models.Requests;

[Route("/api/balance/currency", "GET")]
[Route("/api/balance/currency/{CurrencyCode}", "GET")]
public class CheckCurrencyRequest : IGet, IReturn<MessageResponseBase>
{
    public string CurrencyCode { get; set; }
}