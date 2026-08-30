using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using ThronefallControl.Config;

namespace ThronefallControl.Game;

public sealed class MainThreadTimeoutException : Exception
{
    public MainThreadTimeoutException()
        : base("main_thread_timeout") { }
}

public sealed class MainThread
{
    public static MainThread? Current { get; set; }

    readonly ConcurrentQueue<IWorkItem> _queue = new();
    readonly TimeSpan _defaultTimeout;

    public MainThread(TimeSpan? defaultTimeout = null)
    {
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromMilliseconds(Math.Max(1, PluginConfig.MainThreadTimeoutMs));
    }

    public int QueueDepth => _queue.Count;

    public Task<T> Run<T>(Func<T> work, TimeSpan? timeout = null)
    {
        var item = new WorkItem<T>(work);
        _queue.Enqueue(item);
        item.ScheduleTimeout(timeout ?? _defaultTimeout);
        return item.Task;
    }

    public Task Run(Action work, TimeSpan? timeout = null)
    {
        return Run(() =>
        {
            work();
            return 0;
        }, timeout);
    }

    public void Pump()
    {
        var budget = Math.Max(1, PluginConfig.MaxWorkItemsPerFrame);
        var n = 0;
        while (n < budget && _queue.TryDequeue(out var item))
        {
            n++;
            item.Execute();
        }
    }

    public static void PumpCurrent() => Current?.Pump();

    interface IWorkItem
    {
        void Execute();
    }

    sealed class WorkItem<T> : IWorkItem
    {
        readonly Func<T> _work;
        readonly TaskCompletionSource<T> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public WorkItem(Func<T> work) => _work = work;

        public Task<T> Task => _tcs.Task;

        public void ScheduleTimeout(TimeSpan timeout)
        {
            _ = Expire(timeout);
        }

        async Task Expire(TimeSpan timeout)
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(timeout).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            _tcs.TrySetException(new MainThreadTimeoutException());
        }

        public void Execute()
        {
            try
            {
                _tcs.TrySetResult(_work());
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
            }
        }
    }
}
