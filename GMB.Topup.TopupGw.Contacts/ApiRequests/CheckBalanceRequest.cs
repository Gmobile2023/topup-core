using GMB.Topup.Shared;
using ServiceStack;

namespace GMB.Topup.TopupGw.Contacts.ApiRequests;

[Route("/api/v1/balance", "GET")]
public class CheckBalanceRequest : IGet, IReturn<NewMessageResponseBase<string>>
{
    public string ProviderCode { get; set; }
}