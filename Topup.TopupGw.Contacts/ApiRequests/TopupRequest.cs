using Topup.Shared;
using ServiceStack;

namespace Topup.TopupGw.Contacts.ApiRequests;

[Route("/api/v1/topup", "POST")]
public class TopupRequest : IPost, IReturn<NewMessageResponseBase<string>>
{
    public string MobileNumber { get; set; }
    public decimal Amount { get; set; }
    public string Vendor { get; set; }
    public string TransRef { get; set; }
    public string ProviderCode { get; set; }
    public string ProductCode { get; set; }
}