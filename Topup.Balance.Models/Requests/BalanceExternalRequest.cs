using System.Runtime.Serialization;
using ServiceStack;

namespace Topup.Balance.Models.Requests;

[Route("/api/v1/partner/balance", "GET")]
public class PartnerBalanceGetRequest : IGet, IReturn<object>
{
    [DataMember(Name = "partner")]
    public string PartnerCode { get; set; }
}