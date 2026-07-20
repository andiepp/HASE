using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class EndpointConnectionSupervisionLifetimeTests
{
    [Fact]
    public void Constructor_NullSupervisionDelegate_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new EndpointConnectionSupervisionLifetime(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task RunAsync_RepeatedCall_ShouldUseOneSupervisionTask()
    {
        // Arrange
        var started =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int runCount =
            0;

        async Task RunSupervisionAsync(
            CancellationToken cancellationToken)
        {
            runCount++;

            started.TrySetResult();

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }

        await using var lifetime =
            new EndpointConnectionSupervisionLifetime(
                RunSupervisionAsync);

        // Act
        Task firstTask =
            lifetime.RunAsync();

        Task secondTask =
            lifetime.RunAsync();

        await started.Task;

        // Assert
        Assert.Same(
            firstTask,
            secondTask);

        Assert.Equal(
            1,
            runCount);

        Assert.False(
            firstTask.IsCompleted);
    }

    [Fact]
    public async Task StopAsync_RunningSupervision_ShouldCancelAndComplete()
    {
        // Arrange
        var started =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        CancellationToken receivedToken =
            default;

        async Task RunSupervisionAsync(
            CancellationToken cancellationToken)
        {
            receivedToken =
                cancellationToken;

            started.TrySetResult();

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }

        await using var lifetime =
            new EndpointConnectionSupervisionLifetime(
                RunSupervisionAsync);

        Task supervisionTask =
            lifetime.RunAsync();

        await started.Task;

        // Act
        await lifetime.StopAsync();

        // Assert
        Assert.True(
            receivedToken.CanBeCanceled);

        Assert.True(
            receivedToken.IsCancellationRequested);

        Assert.True(
            supervisionTask.IsCanceled);
    }

    [Fact]
    public async Task StopAsync_RepeatedCall_ShouldBeSafe()
    {
        // Arrange
        var started =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        async Task RunSupervisionAsync(
            CancellationToken cancellationToken)
        {
            started.TrySetResult();

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }

        await using var lifetime =
            new EndpointConnectionSupervisionLifetime(
                RunSupervisionAsync);

        _ = lifetime.RunAsync();

        await started.Task;

        // Act
        await lifetime.StopAsync();
        await lifetime.StopAsync();

        // Assert
        await lifetime.StopAsync();
    }

    [Fact]
    public async Task RunAsync_AfterStop_ShouldThrow()
    {
        // Arrange
        await using var lifetime =
            new EndpointConnectionSupervisionLifetime(
                static cancellationToken =>
                    Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken));

        await lifetime.StopAsync();

        // Act
        void Act()
        {
            _ = lifetime.RunAsync();
        }

        // Assert
        Assert.Throws<InvalidOperationException>(
            Act);
    }

    [Fact]
    public async Task DisposeAsync_RepeatedCall_ShouldBeSafe()
    {
        // Arrange
        var lifetime =
            new EndpointConnectionSupervisionLifetime(
                static cancellationToken =>
                    Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken));

        _ = lifetime.RunAsync();

        // Act
        await lifetime.DisposeAsync();
        await lifetime.DisposeAsync();

        // Assert
        await lifetime.StopAsync();
    }
}