using System.Runtime.Serialization;
using HLS.Paygate.Shared;
using ServiceStack;

namespace Paygate.Discovery.Requests.Backends;

[DataContract]
[Route("/api/v1/refund", "POST")]
[Route("/api/v1/refund/{TransCode}", "POST")]
public class TransactionRefundRequest : IPost, IReturn<NewMessageReponseBase<BalanceResponse>>
{
    [DataMember(Order = 1)] public string TransCode { get; set; }
}