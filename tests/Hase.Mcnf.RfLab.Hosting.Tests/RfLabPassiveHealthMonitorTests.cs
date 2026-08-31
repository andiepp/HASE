using Hase.Runtime.Connections;

namespace Hase.Mcnf.RfLab.Hosting.Tests;

public sealed class RfLabPassiveHealthMonitorTests
{
    [Fact]
    public void ProbeInterval_IsFiveSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), RfLabPassiveHealthMonitor.ProbeInterval);
    }

    [Fact]
    public async Task RunAsync_ProbesOnlyWhileReadyAndSwallowsProbeFailures()
    {
        var states = new Queue<EndpointConnectionState>(
        [
            EndpointConnectionState.Ready,
            EndpointConnectionState.Faulted,
            EndpointConnectionState.Reconnecting,
            EndpointConnectionState.Ready
        ]);
        int probeCount = 0;
        int delayCount = 0;
        using var cancellation = new CancellationTokenSource();

        var monitor = new RfLabPassiveHealthMonitor(
            getConnectionState: () =>
                states.TryDequeue(out EndpointConnectionState state)
                    ? state
                    : EndpointConnectionState.Ready,
            probeAsync: _ =>
            {
                probeCount++;
                return probeCount == 1
                    ? Task.FromException(new InvalidDataException("probe failed"))
                    : Task.CompletedTask;
            },
            delayAsync: (delay, token) =>
            {
                Assert.Equal(RfLabPassiveHealthMonitor.ProbeInterval, delay);
                delayCount++;
                if (delayCount > 4)
                {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                }

                return Task.CompletedTask;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => monitor.RunAsync(cancellation.Token));

        // The two non-Ready states were skipped; the failed first probe did
        // not stop the loop.
        Assert.Equal(2, probeCount);
    }

    [Fact]
    public async Task RunAsync_StopsWhenTheProbeReportsCancellation()
    {
        using var cancellation = new CancellationTokenSource();

        var monitor = new RfLabPassiveHealthMonitor(
            getConnectionState: () => EndpointConnectionState.Ready,
            probeAsync: token =>
            {
                cancellation.Cancel();
                return Task.FromCanceled(cancellation.Token);
            },
            delayAsync: (_, _) => Task.CompletedTask);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => monitor.RunAsync(cancellation.Token));
    }
}
