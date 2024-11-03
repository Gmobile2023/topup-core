using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Contacts.ApiRequests;

[Route("/api/v1/balance", "GET")]
public class CheckBalanceRequest : IGet, IReturn<NewMessageReponseBase<string>>
{
    public string ProviderCode { get; set; }
}