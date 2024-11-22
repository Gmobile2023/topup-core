using Topup.Shared;
using ServiceStack;

namespace Topup.TopupGw.Contacts.ApiRequests;

[Route("/api/v1/balance", "GET")]
public class CheckBalanceRequest : IGet, IReturn<NewMessageResponseBase<string>>
{
    public string ProviderCode { get; set; }
}