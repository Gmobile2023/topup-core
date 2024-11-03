using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Contacts.ApiRequests;

[Route("/api/v1/topup/viettel/deposit", "POST")]
public class ViettelDepositRequest : IPost, IReturn<NewMessageReponseBase<string>>
{
    public decimal Amount { get; set; }

    public string ProviderCode { get; set; }
}