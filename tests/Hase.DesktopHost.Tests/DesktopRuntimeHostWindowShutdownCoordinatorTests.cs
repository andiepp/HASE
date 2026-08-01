using Hase.DesktopHost.App.Hosting;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostWindowShutdownCoordinatorTests
{
    [Fact]
    public async Task StopAsync_ConcurrentRequests_ShouldExecuteStopOnce()
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int stopCount = 0;
        var coordinator = new DesktopRuntimeHostWindowShutdownCoordinator(
            _ =>
            {
                stopCount++;
                return completion.Task;
            });

        Task first = coordinator.StopAsync(CancellationToken.None);
        Task second = coordinator.StopAsync(CancellationToken.None);

        Assert.True(coordinator.IsStarted);
        Assert.False(coordinator.IsCompleted);
        Assert.Same(first, second);
        Assert.Equal(1, stopCount);

        completion.SetResult();
        await first;

        Assert.True(coordinator.IsCompleted);
    }

    [Fact]
    public async Task StopAsync_FailedStop_ShouldRemainCompletedAndNotRetry()
    {
        var expected = new InvalidOperationException("Stop failed.");
        int stopCount = 0;
        var coordinator = new DesktopRuntimeHostWindowShutdownCoordinator(
            _ =>
            {
                stopCount++;
                return Task.FromException(expected);
            });

        InvalidOperationException actual =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => coordinator.StopAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.StopAsync(CancellationToken.None));

        Assert.Same(expected, actual);
        Assert.Equal(1, stopCount);
        Assert.True(coordinator.IsCompleted);
    }
}
