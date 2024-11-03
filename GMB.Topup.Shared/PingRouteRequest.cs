using ServiceStack;
using ServiceStack.DataAnnotations;

namespace GMB.Topup.Shared;

[Exclude(Feature.Metadata)]
[Route("/ping", "GET")]
public class PingRouteRequest
{
}