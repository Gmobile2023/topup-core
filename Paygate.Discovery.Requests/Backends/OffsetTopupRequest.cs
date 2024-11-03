using HLS.Paygate.Shared;
using ServiceStack;

namespace Paygate.Discovery.Requests.Backends;

[Route("/api/v1/OffsetTopup", "POST")]
[Route("/api/v1/OffsetTopup/{TransCode}", "POST")]
public class OffsetTopupRequest : IPost, IReturn<NewMessageReponseBase<BalanceResponse>>
{
    public string TransCode { get; set; }
}


[Route("/api/v1/worker-test", "POST")]
public class Worker_Test_Request : IPost, IReturn<NewMessageReponseBase<string>>
{
    public string Data { get; set; }
}