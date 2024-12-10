using System.Runtime.Serialization;
using Topup.Shared;
using ServiceStack;

namespace Topup.Balance.Models.Requests;

[Route("/api/v1/partner/balance/{partner}", "GET")]
public class PartnerBalanceGetRequest : IGet, IReturn<object>
{
    [DataMember(Name = "partner")]public string PartnerCode { get; set; }
}