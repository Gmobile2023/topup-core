using Topup.Shared;
using ServiceStack;

namespace Topup.Discovery.Requests.Backends;

[Route("/api/v1/OffsetTopup", "POST")]
[Route("/api/v1/OffsetTopup/{TransCode}", "POST")]
public class OffsetTopupRequest : IPost, IReturn<NewMessageResponseBase<BalanceResponse>>
{
    public string TransCode { get; set; }
}


[Route("/api/v1/worker-test", "POST")]
public class Worker_Test_Request : IPost, IReturn<NewMessageResponseBase<string>>
{
    public string Data { get; set; }
}