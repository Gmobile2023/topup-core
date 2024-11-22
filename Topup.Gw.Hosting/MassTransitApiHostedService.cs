using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Topup.Gw.Hosting;

public class MassTransitApiHostedService :
    IHostedService
{
    private readonly IBusControl _bus;
    private readonly ILogger _logger;

    public MassTransitApiHostedService(IBusControl bus, ILoggerFactory loggerFactory)
    {
        _bus = bus;
        _logger = loggerFactory.CreateLogger<MassTransitApiHostedService>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting bus");
        await _bus.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping bus");
        return _bus.StopAsync(cancellationToken);
    }
}