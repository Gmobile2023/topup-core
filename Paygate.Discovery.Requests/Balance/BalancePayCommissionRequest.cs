using HLS.Paygate.Shared;
using ServiceStack;
using System.Runtime.Serialization;

namespace Paygate.Discovery.Requests.Balance;
[DataContract]
[Route("/api/v1/balance/pay-commission", "POST")]
public class BalancePayCommissionRequest : IPost, IReturn<NewMessageReponseBase<BalanceResponse>>
{
    [DataMember(Order = 1)] public string AccountCode { get; set; }
    [DataMember(Order = 2)] public string CurrencyCode { get; set; }
    [DataMember(Order = 3)] public decimal Amount { get; set; }
    [DataMember(Order = 4)] public string TransRef { get; set; }
    [DataMember(Order = 5)] public string TransNote { get; set; }
    [DataMember(Order = 6)] public string ExtraInfo { get; set; }
}