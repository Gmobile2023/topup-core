using GMB.Topup.Shared;
using ServiceStack;
using System.Runtime.Serialization;

namespace GMB.Topup.Discovery.Requests.Balance;

[DataContract]
[Route("/api/v1/balance/cancelPayment", "POST")]
public class BalanceCancelPaymentRequest : IPost, IReturn<NewMessageResponseBase<BalanceResponse>>
{
    [DataMember(Order = 1)] public string TransactionCode { get; set; }
    [DataMember(Order = 2)] public decimal RevertAmount { get; set; }
    [DataMember(Order = 3)] public string TransRef { get; set; }
    [DataMember(Order = 4)] public string TransNote { get; set; }
    [DataMember(Order = 5)] public string Description { get; set; }
    [DataMember(Order = 6)] public string CurrencyCode { get; set; }
    [DataMember(Order = 7)] public string AccountCode { get; set; }
}