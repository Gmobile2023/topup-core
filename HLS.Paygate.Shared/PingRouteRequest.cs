using ServiceStack;
using ServiceStack.DataAnnotations;

namespace HLS.Paygate.Shared;

[Exclude(Feature.Metadata)]
[Route("/ping", "GET")]
public class PingRouteRequest
{
}