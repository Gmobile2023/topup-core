using System.Runtime.Serialization;
using Topup.Shared;
using ServiceStack;

namespace Topup.Discovery.Requests.Backends;

[DataContract]
[Route("/api/v1/refund", "POST")]
[Route("/api/v1/refund/{TransCode}", "POST")]
public class TransactionRefundRequest : IPost, IReturn<NewMessageResponseBase<BalanceResponse>>
{
    [DataMember(Order = 1)] public string TransCode { get; set; }
}