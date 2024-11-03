using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.Gw.Model;

[Route("/api/v1/card/card_check", "GET")]
public class CardCheck : IGet, IReturn<CardResponseMesssage>
{
    public string TransCode { get; set; }
    public string PartnerCode { get; set; }
}