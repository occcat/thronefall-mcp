using ThronefallControl.Config;
using ThronefallControl.Game;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class MainThreadTests
{
    [Fact]
    public async Task Run_completes_when_pumped()
    {
        var mt = new MainThread();
        var task = mt.Run(() => 41 + 1);
        Assert.False(task.IsCompleted);
        mt.Pump();
        Assert.Equal(42, await task);
    }

    [Fact]
    public async Task Run_times_out_if_not_pumped()
    {
        var mt = new MainThread(TimeSpan.FromMilliseconds(40));
        await Assert.ThrowsAsync<MainThreadTimeoutException>(() => mt.Run(() => 1));
    }

    [Fact]
    public async Task Pump_boxes_exceptions()
    {
        var mt = new MainThread();
        var task = mt.Run<int>(() => throw new InvalidOperationException("boom"));
        mt.Pump();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task Fake_dispatcher_pumps_until_complete()
    {
        var mt = new MainThread();
        using var cts = new CancellationTokenSource();
        var dispatcher = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                mt.Pump();
                try
                {
                    await Task.Delay(5, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        });

        try
        {
            Assert.Equal(7, await mt.Run(() => 7));
        }
        finally
        {
            cts.Cancel();
            try { await dispatcher; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public void Pump_respects_per_frame_budget()
    {
        var previous = PluginConfig.MaxWorkItemsPerFrame;
        PluginConfig.MaxWorkItemsPerFrame = 2;
        try
        {
            var mt = new MainThread(TimeSpan.FromSeconds(2));
            var a = mt.Run(() => 1);
            var b = mt.Run(() => 2);
            var c = mt.Run(() => 3);
            mt.Pump();
            Assert.True(a.IsCompleted);
            Assert.True(b.IsCompleted);
            Assert.False(c.IsCompleted);
            mt.Pump();
            Assert.True(c.IsCompleted);
        }
        finally
        {
            PluginConfig.MaxWorkItemsPerFrame = previous;
        }
    }
}
