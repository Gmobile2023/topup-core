using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Topup.Shared;

[Exclude(Feature.Metadata)]
[Route("/ping", "GET")]
public class PingRouteRequest
{
}