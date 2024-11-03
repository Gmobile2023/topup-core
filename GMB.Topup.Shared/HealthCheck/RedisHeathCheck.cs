using System;
using System.Threading;
using System.Threading.Tasks;
using GMB.Topup.Shared.CacheManager;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GMB.Topup.Shared.HealthCheck;

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