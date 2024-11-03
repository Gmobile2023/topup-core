using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Contacts.ApiRequests;

[Route("/api/v1/topup", "POST")]
public class TopupRequest : IPost, IReturn<NewMessageReponseBase<string>>
{
    public string MobileNumber { get; set; }
    public decimal Amount { get; set; }
    public string Vendor { get; set; }
    public string TransRef { get; set; }
    public string ProviderCode { get; set; }
    public string ProductCode { get; set; }
}