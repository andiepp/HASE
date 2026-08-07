using Hase.Runtime.Connections;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103PassiveHealthMonitorTests
{
    [Fact]
    public async Task RunAsync_DelaysBeforeFirstProbeAndUsesFixedInterval()
    {
        var delay = new ControlledDelay();
        var probeCompleted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int probeCount = 0;
        var monitor = new Kel103PassiveHealthMonitor(
            () => EndpointConnectionState.Ready,
            _ =>
            {
                probeCount++;
                probeCompleted.TrySetResult();
                return Task.CompletedTask;
            },
            delay.DelayAsync);
        using var cancellation = new CancellationTokenSource();

        Task run = monitor.RunAsync(cancellation.Token);
        ControlledDelay.Request first = await delay.NextAsync();

        Assert.Equal(TimeSpan.FromSeconds(5), first.Duration);
        Assert.Equal(0, probeCount);

        first.Complete();
        await probeCompleted.Task;
        ControlledDelay.Request second = await delay.NextAsync();

        Assert.Equal(1, probeCount);
        Assert.Equal(TimeSpan.FromSeconds(5), second.Duration);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task RunAsync_NonReadyStateSkipsProbe()
    {
        var delay = new ControlledDelay();
        int probeCount = 0;
        EndpointConnectionState state = EndpointConnectionState.Faulted;
        var monitor = new Kel103PassiveHealthMonitor(
            () => state,
            _ =>
            {
                probeCount++;
                return Task.CompletedTask;
            },
            delay.DelayAsync);
        using var cancellation = new CancellationTokenSource();

        Task run = monitor.RunAsync(cancellation.Token);
        (await delay.NextAsync()).Complete();
        ControlledDelay.Request second = await delay.NextAsync();

        Assert.Equal(0, probeCount);

        state = EndpointConnectionState.Ready;
        second.Complete();
        await WaitUntilAsync(() => probeCount == 1);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task RunAsync_DoesNotScheduleNextIntervalUntilProbeCompletes()
    {
        var delay = new ControlledDelay();
        var probeStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProbe = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var monitor = new Kel103PassiveHealthMonitor(
            () => EndpointConnectionState.Ready,
            async token =>
            {
                probeStarted.TrySetResult();
                await releaseProbe.Task.WaitAsync(token);
            },
            delay.DelayAsync);
        using var cancellation = new CancellationTokenSource();

        Task run = monitor.RunAsync(cancellation.Token);
        (await delay.NextAsync()).Complete();
        await probeStarted.Task;

        Assert.Equal(0, delay.PendingRequestCount);

        releaseProbe.SetResult();
        _ = await delay.NextAsync();

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task RunAsync_ProbeFailureLeavesMonitorAvailableForLaterReadyState()
    {
        var delay = new ControlledDelay();
        var probeOccurred = new SemaphoreSlim(0);
        EndpointConnectionState state = EndpointConnectionState.Ready;
        int probeCount = 0;
        var monitor = new Kel103PassiveHealthMonitor(
            () => state,
            _ =>
            {
                probeCount++;
                probeOccurred.Release();
                if (probeCount == 1)
                {
                    state = EndpointConnectionState.Faulted;
                    throw new IOException("simulated probe failure");
                }

                return Task.CompletedTask;
            },
            delay.DelayAsync);
        using var cancellation = new CancellationTokenSource();

        Task run = monitor.RunAsync(cancellation.Token);
        (await delay.NextAsync()).Complete();
        await probeOccurred.WaitAsync();
        ControlledDelay.Request second = await delay.NextAsync();

        Assert.False(run.IsCompleted);
        Assert.Equal(1, probeCount);

        state = EndpointConnectionState.Ready;
        second.Complete();
        await probeOccurred.WaitAsync();

        Assert.Equal(2, probeCount);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task RunAsync_CancellationDuringDelayStopsWithoutProbe()
    {
        var delay = new ControlledDelay();
        int probeCount = 0;
        var monitor = new Kel103PassiveHealthMonitor(
            () => EndpointConnectionState.Ready,
            _ =>
            {
                probeCount++;
                return Task.CompletedTask;
            },
            delay.DelayAsync);
        using var cancellation = new CancellationTokenSource();

        Task run = monitor.RunAsync(cancellation.Token);
        _ = await delay.NextAsync();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(0, probeCount);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(1, timeout.Token);
        }
    }

    private sealed class ControlledDelay
    {
        private readonly object sync = new();
        private readonly Queue<Request> pending = [];
        private readonly SemaphoreSlim available = new(0);

        public int PendingRequestCount
        {
            get
            {
                lock (sync)
                {
                    return pending.Count;
                }
            }
        }

        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (sync)
            {
                pending.Enqueue(new Request(duration, completion));
            }

            available.Release();
            return completion.Task.WaitAsync(cancellationToken);
        }

        public async Task<Request> NextAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await available.WaitAsync(timeout.Token);
            lock (sync)
            {
                return pending.Dequeue();
            }
        }

        public sealed class Request(
            TimeSpan duration,
            TaskCompletionSource completion)
        {
            public TimeSpan Duration { get; } = duration;

            public void Complete() => completion.TrySetResult();
        }
    }
}
