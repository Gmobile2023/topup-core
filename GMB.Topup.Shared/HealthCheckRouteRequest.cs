using ServiceStack;
using ServiceStack.DataAnnotations;

namespace GMB.Topup.Shared;

[Route("/health-check-notifi", "POST")]
public class HealthCheckNotifiRequest
{
    public string message { get; set; }
    public object payload { get; set; }
    public object restorePayload { get; set; }
}