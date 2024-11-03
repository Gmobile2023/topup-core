using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace HLS.Paygate.Worker.Components.TaskQueues;

public interface IBackgroundTaskQueue2
{
    void QueueBackgroundWorkItem(Func<Task> workItem);

    Task<Func<Task>> DequeueAsync();
}

public class BackgroundTaskQueue2 : IBackgroundTaskQueue2
{
    private readonly ConcurrentQueue<Func<Task>> _workItems = new();
    private readonly SemaphoreSlim _signal = new(1000);

    public void QueueBackgroundWorkItem(
        Func<Task> workItem)
    {
        if (workItem == null)
        {
            throw new ArgumentNullException(nameof(workItem));
        }
        _workItems.Enqueue(workItem);
        _signal.Release();
    }

    public async Task<Func<Task>> DequeueAsync()
    {
        Console.WriteLine("DequeueAsync");
        await _signal.WaitAsync();
        _workItems.TryDequeue(out var workItem);

        return workItem;
    }
}