using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Topup.Shared.HealthCheck;

public class RabbitMqHeathCheck : IHealthCheck
{
    private readonly IBus _bus;

    public RabbitMqHeathCheck(IBus bus)
    {
        _bus = bus;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            return Task.FromResult(HealthCheckResult.Healthy("The rabbitMq check is healthy"));
        }
        catch (Exception e)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(e.Message));
        }
    }
}