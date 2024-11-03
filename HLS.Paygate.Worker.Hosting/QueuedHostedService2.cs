using System;
using System.Threading;
using System.Threading.Tasks;
using HLS.Paygate.Worker.Components.TaskQueues;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HLS.Paygate.Worker.Hosting;

public class QueuedHostedService2 : BackgroundService
{
    private readonly ILogger<QueuedHostedService2> _logger;
        
    public QueuedHostedService2(IBackgroundTaskQueue2 taskQueue, 
        ILogger<QueuedHostedService2> logger)
    {
        TaskQueue = taskQueue;
        _logger = logger;
    }

    private IBackgroundTaskQueue2 TaskQueue { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            $"Queued Hosted Service is running.{Environment.NewLine}{Environment.NewLine}");

        await BackgroundProcessing(stoppingToken);
    }

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = 
                await TaskQueue.DequeueAsync();
                
            if (workItem is null)
                continue;

            try
            {
                await workItem();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error occurred executing {WorkItem}", nameof(workItem));
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queued Hosted Service is stopping.");

        await base.StopAsync(stoppingToken);
    }
}