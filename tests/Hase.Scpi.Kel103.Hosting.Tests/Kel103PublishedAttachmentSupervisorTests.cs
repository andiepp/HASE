using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103PublishedAttachmentSupervisorTests
{
    [Fact]
    public async Task RunAsync_ReadyEndpointPerformsNoReplacementOrDelay()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var replaceCount = 0;
        var delayCount = 0;
        var supervisor = CreateSupervisor(
            endpoint,
            (options, token) =>
            {
                replaceCount++;
                return Task.CompletedTask;
            },
            new DefaultRuntimeEndpointReconnectPolicy(),
            (delay, token) =>
            {
                delayCount++;
                return Task.CompletedTask;
            });
        using var cancellation = new CancellationTokenSource();

        Task run = supervisor.RunAsync(cancellation.Token);
        await Task.Yield();

        Assert.Equal(0, replaceCount);
        Assert.Equal(0, delayCount);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task RunAsync_FaultUsesExactDefaultScheduleAndStopsAfterRecovery()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var policy = new RecordingReconnectPolicy();
        var delays = new List<TimeSpan>();
        var replacementCount = 0;
        var recovered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var supervisor = CreateSupervisor(
            endpoint,
            (options, token) =>
            {
                replacementCount++;
                if (replacementCount < 6)
                {
                    throw new IOException("scripted replacement failure");
                }

                endpoint.UpdateConnectionStatus(
                    new EndpointConnectionStatus(EndpointConnectionState.Ready));
                recovered.TrySetResult();
                return Task.CompletedTask;
            },
            policy,
            (delay, token) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });
        using var cancellation = new CancellationTokenSource();
        Task run = supervisor.RunAsync(cancellation.Token);

        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));
        await recovered.Task;

        Assert.Equal([0, 1, 2, 3, 4, 5], policy.Attempts);
        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10)],
            delays);
        Assert.Equal(6, replacementCount);
        Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task RunAsync_LaterFaultRestartsWithImmediateAttempt()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var policy = new RecordingReconnectPolicy();
        var replacementCount = 0;
        var secondRecovery = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var supervisor = CreateSupervisor(
            endpoint,
            (options, token) =>
            {
                replacementCount++;
                endpoint.UpdateConnectionStatus(
                    new EndpointConnectionStatus(EndpointConnectionState.Ready));
                if (replacementCount == 2)
                {
                    secondRecovery.TrySetResult();
                }

                return Task.CompletedTask;
            },
            policy,
            (delay, token) => Task.CompletedTask);
        using var cancellation = new CancellationTokenSource();
        Task run = supervisor.RunAsync(cancellation.Token);

        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));
        await WaitUntilAsync(() =>
            replacementCount == 1
            && endpoint.ConnectionStatus.State == EndpointConnectionState.Ready);
        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));
        await secondRecovery.Task;

        Assert.Equal([0, 0], policy.Attempts);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task RunAsync_RepeatedFaultNotificationsNeverOverlapReplacement()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementCount = 0;
        var activeCount = 0;
        var maximumActiveCount = 0;
        var supervisor = CreateSupervisor(
            endpoint,
            async (options, token) =>
            {
                replacementCount++;
                activeCount++;
                maximumActiveCount = Math.Max(maximumActiveCount, activeCount);
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
                activeCount--;
                endpoint.UpdateConnectionStatus(
                    new EndpointConnectionStatus(EndpointConnectionState.Ready));
            },
            new DefaultRuntimeEndpointReconnectPolicy(),
            (delay, token) => Task.CompletedTask);
        using var cancellation = new CancellationTokenSource();
        Task run = supervisor.RunAsync(cancellation.Token);

        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));
        await entered.Task;
        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Faulted,
                detail: "sanitized repeated signal"));
        release.TrySetResult();
        await WaitUntilAsync(() => endpoint.ConnectionStatus.State == EndpointConnectionState.Ready);
        await Task.Yield();

        Assert.Equal(1, replacementCount);
        Assert.Equal(1, maximumActiveCount);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task RunAsync_ShutdownCancelsPendingReconnectDelay()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var delayStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementCount = 0;
        var supervisor = CreateSupervisor(
            endpoint,
            (options, token) =>
            {
                replacementCount++;
                throw new IOException("scripted replacement failure");
            },
            new DefaultRuntimeEndpointReconnectPolicy(),
            async (delay, token) =>
            {
                delayStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            });
        using var cancellation = new CancellationTokenSource();
        Task run = supervisor.RunAsync(cancellation.Token);

        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));
        await delayStarted.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(1, replacementCount);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        Func<SerialTransportOptions, CancellationToken, Task> replace =
            static (options, token) => Task.CompletedTask;
        Func<TimeSpan, CancellationToken, Task> delay =
            static (value, token) => Task.CompletedTask;

        Assert.Throws<ArgumentNullException>(() =>
            new Kel103PublishedAttachmentSupervisor(
                null!, replace, SupportedOptions(), new DefaultRuntimeEndpointReconnectPolicy(), delay));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103PublishedAttachmentSupervisor(
                endpoint, null!, SupportedOptions(), new DefaultRuntimeEndpointReconnectPolicy(), delay));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103PublishedAttachmentSupervisor(
                endpoint, replace, null!, new DefaultRuntimeEndpointReconnectPolicy(), delay));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103PublishedAttachmentSupervisor(
                endpoint, replace, SupportedOptions(), null!, delay));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103PublishedAttachmentSupervisor(
                endpoint, replace, SupportedOptions(), new DefaultRuntimeEndpointReconnectPolicy(), null!));
    }

    private static Kel103PublishedAttachmentSupervisor CreateSupervisor(
        RuntimeEndpoint endpoint,
        Func<SerialTransportOptions, CancellationToken, Task> replaceAsync,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        Func<TimeSpan, CancellationToken, Task> delayAsync) =>
        new(endpoint, replaceAsync, SupportedOptions(), reconnectPolicy, delayAsync);

    private static RuntimeEndpoint ReadyEndpoint()
    {
        RuntimeEndpoint endpoint = new RuntimeContext().CreateEndpoint(
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Materialize(
                new EndpointId("supervision-test")));
        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Ready));
        return endpoint;
    }

    private static SerialTransportOptions SupportedOptions() =>
        new("TEST-PORT", 115200);

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(1, timeout.Token);
        }
    }

    private sealed class RecordingReconnectPolicy : IRuntimeEndpointReconnectPolicy
    {
        private readonly DefaultRuntimeEndpointReconnectPolicy inner = new();

        public List<int> Attempts { get; } = [];

        public TimeSpan GetDelay(int retryAttempt)
        {
            Attempts.Add(retryAttempt);
            return inner.GetDelay(retryAttempt);
        }
    }
}
