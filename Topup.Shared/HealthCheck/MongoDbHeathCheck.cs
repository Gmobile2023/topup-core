using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDbGenericRepository;

namespace HLS.Paygate.Shared.HealthCheck;

public abstract class MongoDbHeathCheck : BaseMongoRepository, IHealthCheck
{
    protected MongoDbHeathCheck(IMongoDbContext dbContext) : base(dbContext)
    {
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            return Task.FromResult(MongoDbContext.Database.DatabaseNamespace.DatabaseName != null
                ? HealthCheckResult.Healthy("The mongodb check is healthy")
                : HealthCheckResult.Unhealthy("The mongodb check is unhealthy"));
        }
        catch (Exception e)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "The mongodb check is unhealthy" + e.Message));
        }
    }
}