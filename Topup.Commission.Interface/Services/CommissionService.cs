using System.Threading.Tasks;
using Topup.Shared;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace Topup.Commission.Interface.Services;

public class CommissionService : Service
{
    private readonly ILogger<CommissionService> _logger;


    public CommissionService(ILogger<CommissionService> logger)
    {
        _logger = logger;
    }

    public async Task<object> GetAsync(PingRouteRequest request)
    {
        return await Task.FromResult("OK");
    }
}