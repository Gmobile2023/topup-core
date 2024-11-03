using GMB.Topup.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace GMB.Topup.Gw.Model.RequestDtos;

[Route("/api/v1/topuptransaction", "POST")]
public class TopupProviderRequest : IPost, IReturn<MessageResponseBase>
{
    [Required] public string ReceiveAccount { get; set; }

    [Required] public int Amount { get; set; }

    [Required] public string TransCode { get; set; }

    [Required] public string PartnerCode { get; set; }

    [Required] public string CategoryCode { get; set; }
}