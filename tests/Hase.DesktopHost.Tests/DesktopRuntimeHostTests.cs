namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostTests
{
    [Fact]
    public async Task StartAsync_ShouldTransitionFromStoppedThroughStartingToRunning()
    {
        var backend = new RecordingBackend();
        var host = new DesktopRuntimeHost(backend);
        var transitions = RecordTransitions(host);

        await host.StartAsync();

        Assert.Equal(DesktopRuntimeHostStatus.Running, host.Status);
        Assert.Null(host.LastError);
        Assert.Equal(1, backend.StartCount);
        Assert.Equal(
            [
                (DesktopRuntimeHostStatus.Stopped, DesktopRuntimeHostStatus.Starting),
                (DesktopRuntimeHostStatus.Starting, DesktopRuntimeHostStatus.Running)
            ],
            transitions);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ShouldNotStartBackendAgain()
    {
        var backend = new RecordingBackend();
        var host = new DesktopRuntimeHost(backend);

        await host.StartAsync();
        await host.StartAsync();

        Assert.Equal(DesktopRuntimeHostStatus.Running, host.Status);
        Assert.Equal(1, backend.StartCount);
    }

    [Fact]
    public async Task StopAsync_ShouldTransitionFromRunningThroughStoppingToStopped()
    {
        var backend = new RecordingBackend();
        var host = new DesktopRuntimeHost(backend);
        await host.StartAsync();
        var transitions = RecordTransitions(host);

        await host.StopAsync();

        Assert.Equal(DesktopRuntimeHostStatus.Stopped, host.Status);
        Assert.Null(host.LastError);
        Assert.Equal(1, backend.StopCount);
        Assert.Equal(
            [
                (DesktopRuntimeHostStatus.Running, DesktopRuntimeHostStatus.Stopping),
                (DesktopRuntimeHostStatus.Stopping, DesktopRuntimeHostStatus.Stopped)
            ],
            transitions);
    }

    [Fact]
    public async Task StopAsync_WhenAlreadyStopped_ShouldNotStopBackend()
    {
        var backend = new RecordingBackend();
        var host = new DesktopRuntimeHost(backend);

        await host.StopAsync();

        Assert.Equal(DesktopRuntimeHostStatus.Stopped, host.Status);
        Assert.Equal(0, backend.StopCount);
    }

    [Fact]
    public async Task StartAsync_WhenBackendFails_ShouldTransitionToFaultedAndPreserveError()
    {
        var expected = new InvalidOperationException("Start failed.");
        var backend = new RecordingBackend { StartException = expected };
        var host = new DesktopRuntimeHost(backend);
        var transitions = RecordTransitions(host);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.StartAsync());

        Assert.Same(expected, actual);
        Assert.Same(expected, host.LastError);
        Assert.Equal(DesktopRuntimeHostStatus.Faulted, host.Status);
        Assert.Equal(
            [
                (DesktopRuntimeHostStatus.Stopped, DesktopRuntimeHostStatus.Starting),
                (DesktopRuntimeHostStatus.Starting, DesktopRuntimeHostStatus.Faulted)
            ],
            transitions);
    }

    [Fact]
    public async Task StopAsync_WhenBackendFails_ShouldTransitionToFaultedAndPreserveError()
    {
        var expected = new InvalidOperationException("Stop failed.");
        var backend = new RecordingBackend { StopException = expected };
        var host = new DesktopRuntimeHost(backend);
        await host.StartAsync();
        var transitions = RecordTransitions(host);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.StopAsync());

        Assert.Same(expected, actual);
        Assert.Same(expected, host.LastError);
        Assert.Equal(DesktopRuntimeHostStatus.Faulted, host.Status);
        Assert.Equal(
            [
                (DesktopRuntimeHostStatus.Running, DesktopRuntimeHostStatus.Stopping),
                (DesktopRuntimeHostStatus.Stopping, DesktopRuntimeHostStatus.Faulted)
            ],
            transitions);
    }

    [Fact]
    public async Task DisposeAsync_WhenRunning_ShouldStopBackend()
    {
        var backend = new RecordingBackend();
        var host = new DesktopRuntimeHost(backend);
        await host.StartAsync();

        await host.DisposeAsync();

        Assert.Equal(DesktopRuntimeHostStatus.Stopped, host.Status);
        Assert.Equal(1, backend.StopCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.StartAsync());
    }

    private static List<(DesktopRuntimeHostStatus Previous, DesktopRuntimeHostStatus Current)>
        RecordTransitions(DesktopRuntimeHost host)
    {
        var transitions = new List<(
            DesktopRuntimeHostStatus Previous,
            DesktopRuntimeHostStatus Current)>();

        host.StatusChanged += (_, eventArgs) =>
            transitions.Add((eventArgs.PreviousStatus, eventArgs.CurrentStatus));

        return transitions;
    }

    private sealed class RecordingBackend : IDesktopRuntimeHostBackend
    {
        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public Exception? StartException { get; init; }

        public Exception? StopException { get; init; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;

            return StartException is null
                ? Task.CompletedTask
                : Task.FromException(StartException);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;

            return StopException is null
                ? Task.CompletedTask
                : Task.FromException(StopException);
        }
    }
}
