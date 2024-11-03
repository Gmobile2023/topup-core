using System;
using System.Threading;
using System.Threading.Tasks;
using GMB.Topup.Worker.Components.TaskQueues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GMB.Topup.Worker.Hosting
{
    public class QueuedHostedService : BackgroundService
    {
        private readonly ILogger<QueuedHostedService> _logger;
        private readonly Task[] _executors;
        private readonly int _executorsCount = 50; //--default value: 50
        private CancellationTokenSource _tokenSource;

        public QueuedHostedService(IBackgroundTaskQueue taskQueue, 
            ILogger<QueuedHostedService> logger, IConfiguration configuration)
        {
            TaskQueue = taskQueue;
            _logger = logger;
        

            if (ushort.TryParse(configuration["WorkerConfig:MaxNumOfParallelBackgroundOperations"], out var ct))
            {
                _executorsCount = ct;
            }
            _executors = new Task[_executorsCount];
        }

        public IBackgroundTaskQueue TaskQueue { get; }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is starting.");

            _tokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            for (var i = 0; i < _executorsCount; i++)
            {
                var executorTask = new Task(
                    async () =>
                    {
                        while (!stoppingToken.IsCancellationRequested)
                        {
#if DEBUG
                            _logger.LogInformation("Waiting background task...");
#endif
                            await BackgroundProcessing(stoppingToken);
                        }
                    }, _tokenSource.Token);

                _executors[i] = executorTask;
                executorTask.Start();
            }
            
            return Task.CompletedTask;
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem =
                    await TaskQueue.DequeueAsync(stoppingToken);

                try
                {
                    await workItem(stoppingToken);
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
}