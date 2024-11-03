using GMB.Topup.Shared;
using ServiceStack;

namespace GMB.Topup.TopupGw.Contacts.ApiRequests;

[Route("/api/v1/topup/viettel/deposit", "POST")]
public class ViettelDepositRequest : IPost, IReturn<NewMessageResponseBase<string>>
{
    public decimal Amount { get; set; }

    public string ProviderCode { get; set; }
}