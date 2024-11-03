using System.Runtime.Serialization;
using GMB.Topup.Shared;
using ServiceStack;

namespace GMB.Topup.Discovery.Requests.Balance;

[DataContract]
[Route("/api/v1/balance/check", "GET")]
public class AccountBalanceCheckRequest : IGet, IReturn<ResponseMessageApi<decimal>>
{
    [DataMember(Order = 1)] public string AccountCode { get; set; }
    [DataMember(Order = 2)] public string CurrencyCode { get; set; }
}