using Topup.Shared;
using ServiceStack;

namespace Topup.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/card/code", "GET")]
public class CardGetByCode : IGet, IReturn<MessageResponseBase>
{
    public string Code { get; set; }
    public string Vendor { get; set; }
}