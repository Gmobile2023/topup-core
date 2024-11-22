using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Topup.Shared.CacheManager;

namespace Topup.Shared.HealthCheck;

public class RedisHeathCheck : IHealthCheck
{
    private readonly ICacheManager _cacheManager;

    public RedisHeathCheck(ICacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            var testKey = "Test-" + Guid.NewGuid();
            await _cacheManager.SetCache(testKey, "Test", TimeSpan.FromMinutes(1));
            await _cacheManager.GetCache(testKey);
            await _cacheManager.ClearCache(testKey);

            return HealthCheckResult.Healthy("The cache check is healthy");
        }
        catch (Exception e)
        {
            return HealthCheckResult.Unhealthy(
                "The cache check is unhealthy" + e.Message);
        }
    }
}