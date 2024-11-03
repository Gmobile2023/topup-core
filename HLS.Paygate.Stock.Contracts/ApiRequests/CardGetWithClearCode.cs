using System;
using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/cardfull", "GET")]
public class CardGetWithClearCode : IGet, IReturn<MessageResponseBase>
{
    public Guid Id { get; set; }
    public string Serial { get; set; }
    public string Vendor { get; set; }
}