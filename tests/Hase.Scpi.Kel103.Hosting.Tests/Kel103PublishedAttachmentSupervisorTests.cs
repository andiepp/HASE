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
        Assert.Equal(RuntimeEndpointConnectionStatistics.Empty, supervisor.GetStatistics());
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
        await WaitUntilAsync(() => supervisor.GetStatistics().SuccessfulRecoveryCount == 1);

        Assert.Equal([0, 1, 2, 3, 4, 5], policy.Attempts);
        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10)],
            delays);
        Assert.Equal(6, replacementCount);
        Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);
        RuntimeEndpointConnectionStatistics statistics = supervisor.GetStatistics();
        Assert.Equal(6, statistics.ReconnectAttemptCount);
        Assert.Equal(5, statistics.ReconnectFailureCount);
        Assert.Equal(1, statistics.SuccessfulRecoveryCount);

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
        await WaitUntilAsync(() => supervisor.GetStatistics().SuccessfulRecoveryCount == 2);

        Assert.Equal([0, 0], policy.Attempts);
        RuntimeEndpointConnectionStatistics statistics = supervisor.GetStatistics();
        Assert.Equal(2, statistics.ReconnectAttemptCount);
        Assert.Equal(0, statistics.ReconnectFailureCount);
        Assert.Equal(2, statistics.SuccessfulRecoveryCount);
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
        RuntimeEndpointConnectionStatistics statistics = supervisor.GetStatistics();
        Assert.Equal(1, statistics.ReconnectAttemptCount);
        Assert.Equal(1, statistics.ReconnectFailureCount);
        Assert.Equal(0, statistics.SuccessfulRecoveryCount);
    }

    [Fact]
    public async Task GetStatistics_RecordsCompleteRecoveryDurationAcrossFailuresAndDelay()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 3, 21, 0, 0, TimeSpan.Zero));
        var replacementCount = 0;
        var recovered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var supervisor = CreateSupervisor(
            endpoint,
            (options, token) =>
            {
                replacementCount++;
                if (replacementCount == 1)
                {
                    timeProvider.Advance(TimeSpan.FromSeconds(2));
                    throw new IOException("scripted replacement failure");
                }

                timeProvider.Advance(TimeSpan.FromSeconds(3));
                endpoint.UpdateConnectionStatus(
                    new EndpointConnectionStatus(EndpointConnectionState.Ready));
                recovered.TrySetResult();
                return Task.CompletedTask;
            },
            new DefaultRuntimeEndpointReconnectPolicy(),
            (delay, token) =>
            {
                timeProvider.Advance(delay);
                return Task.CompletedTask;
            },
            timeProvider);
        using var cancellation = new CancellationTokenSource();
        Task run = supervisor.RunAsync(cancellation.Token);
        DateTimeOffset started = timeProvider.GetUtcNow();

        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));
        await recovered.Task;
        await WaitUntilAsync(() => supervisor.GetStatistics().SuccessfulRecoveryCount == 1);

        RuntimeEndpointConnectionStatistics statistics = supervisor.GetStatistics();
        Assert.Equal(2, statistics.ReconnectAttemptCount);
        Assert.Equal(1, statistics.ReconnectFailureCount);
        Assert.Equal(1, statistics.SuccessfulRecoveryCount);
        Assert.Equal(started, statistics.LastRecoveryStartedAtUtc);
        Assert.Equal(started.AddSeconds(6), statistics.LastRecoveryCompletedAtUtc);
        Assert.Equal(TimeSpan.FromSeconds(6), statistics.LastRecoveryDuration);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task GetStatistics_ActiveReplacementCancellationCountsAttemptButNotFailure()
    {
        RuntimeEndpoint endpoint = ReadyEndpoint();
        var replacementStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var supervisor = CreateSupervisor(
            endpoint,
            async (options, token) =>
            {
                replacementStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            },
            new DefaultRuntimeEndpointReconnectPolicy(),
            (delay, token) => Task.CompletedTask);
        using var cancellation = new CancellationTokenSource();
        Task run = supervisor.RunAsync(cancellation.Token);

        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));
        await replacementStarted.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        RuntimeEndpointConnectionStatistics statistics = supervisor.GetStatistics();
        Assert.Equal(1, statistics.ReconnectAttemptCount);
        Assert.Equal(0, statistics.ReconnectFailureCount);
        Assert.Equal(0, statistics.SuccessfulRecoveryCount);
        Assert.NotNull(statistics.LastRecoveryStartedAtUtc);
        Assert.Null(statistics.LastRecoveryCompletedAtUtc);
        Assert.Null(statistics.LastRecoveryDuration);
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
                null!, replace, SupportedOptions(), new DefaultRuntimeEndpointReconnectPolicy(), TimeProvider.System, delay));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103PublishedAttachmentSupervisor(
                endpoint, null!, SupportedOptions(), new DefaultRuntimeEndpointReconnectPolicy(), TimeProvider.System, delay));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103PublishedAttachmentSupervisor(
                endpoint, replace, null!, new DefaultRuntimeEndpointReconnectPolicy(), TimeProvider.System, delay));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103PublishedAttachmentSupervisor(
                endpoint, replace, SupportedOptions(), null!, TimeProvider.System, delay));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103PublishedAttachmentSupervisor(
                endpoint, replace, SupportedOptions(), new DefaultRuntimeEndpointReconnectPolicy(), null!, delay));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103PublishedAttachmentSupervisor(
                endpoint, replace, SupportedOptions(), new DefaultRuntimeEndpointReconnectPolicy(), TimeProvider.System, null!));
    }

    private static Kel103PublishedAttachmentSupervisor CreateSupervisor(
        RuntimeEndpoint endpoint,
        Func<SerialTransportOptions, CancellationToken, Task> replaceAsync,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        TimeProvider? timeProvider = null) =>
        new(
            endpoint,
            replaceAsync,
            SupportedOptions(),
            reconnectPolicy,
            timeProvider ?? TimeProvider.System,
            delayAsync);

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

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow;
        private long timestamp;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan duration)
        {
            utcNow = utcNow.Add(duration);
            timestamp += duration.Ticks;
        }
    }
}
