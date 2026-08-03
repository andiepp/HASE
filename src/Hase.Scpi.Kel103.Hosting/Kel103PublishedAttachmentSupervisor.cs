using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting;

internal sealed class Kel103PublishedAttachmentSupervisor
    : IEndpointConnectionStatusObserver
{
    private readonly RuntimeEndpoint runtimeEndpoint;
    private readonly Func<SerialTransportOptions, CancellationToken, Task> replaceAsync;
    private readonly SerialTransportOptions serialOptions;
    private readonly IRuntimeEndpointReconnectPolicy reconnectPolicy;
    private readonly TimeProvider timeProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly SemaphoreSlim statusChanged = new(0);
    private readonly object statisticsLock = new();
    private long reconnectAttemptCount;
    private long reconnectFailureCount;
    private long successfulRecoveryCount;
    private DateTimeOffset? lastRecoveryStartedAtUtc;
    private DateTimeOffset? lastRecoveryCompletedAtUtc;
    private TimeSpan? lastRecoveryDuration;

    public Kel103PublishedAttachmentSupervisor(
        Kel103PublishedAttachment attachment,
        SerialTransportOptions serialOptions,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        TimeProvider timeProvider)
        : this(
            (attachment ?? throw new ArgumentNullException(nameof(attachment))).RuntimeEndpoint,
            attachment.ReplaceAsync,
            serialOptions,
            reconnectPolicy,
            timeProvider,
            CreateDelay(timeProvider))
    {
    }

    internal Kel103PublishedAttachmentSupervisor(
        RuntimeEndpoint runtimeEndpoint,
        Func<SerialTransportOptions, CancellationToken, Task> replaceAsync,
        SerialTransportOptions serialOptions,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        TimeProvider timeProvider,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        this.runtimeEndpoint = runtimeEndpoint
            ?? throw new ArgumentNullException(nameof(runtimeEndpoint));
        this.replaceAsync = replaceAsync
            ?? throw new ArgumentNullException(nameof(replaceAsync));
        this.serialOptions = serialOptions
            ?? throw new ArgumentNullException(nameof(serialOptions));
        this.reconnectPolicy = reconnectPolicy
            ?? throw new ArgumentNullException(nameof(reconnectPolicy));
        this.timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
        this.delayAsync = delayAsync
            ?? throw new ArgumentNullException(nameof(delayAsync));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        runtimeEndpoint.SubscribeConnectionStatus(this);
        try
        {
            while (true)
            {
                await WaitForFaultAsync(cancellationToken).ConfigureAwait(false);
                await RecoverAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            runtimeEndpoint.UnsubscribeConnectionStatus(this);
        }
    }

    public void OnEndpointConnectionStatusChanged(EndpointConnectionStatusChanged change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (ReferenceEquals(change.Endpoint, runtimeEndpoint))
        {
            statusChanged.Release();
        }
    }

    public RuntimeEndpointConnectionStatistics GetStatistics()
    {
        lock (statisticsLock)
        {
            return new RuntimeEndpointConnectionStatistics(
                initialConnectionAttemptCount: 0,
                initialConnectionFailureCount: 0,
                reconnectAttemptCount: reconnectAttemptCount,
                reconnectFailureCount: reconnectFailureCount,
                successfulRecoveryCount: successfulRecoveryCount,
                lastRecoveryStartedAtUtc: lastRecoveryStartedAtUtc,
                lastRecoveryCompletedAtUtc: lastRecoveryCompletedAtUtc,
                lastRecoveryDuration: lastRecoveryDuration);
        }
    }

    private async Task WaitForFaultAsync(CancellationToken cancellationToken)
    {
        while (runtimeEndpoint.ConnectionStatus.State != EndpointConnectionState.Faulted)
        {
            await statusChanged.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset startedAtUtc = timeProvider.GetUtcNow();
        long startedTimestamp = timeProvider.GetTimestamp();
        RecordRecoveryStarted(startedAtUtc);

        int retryAttempt = 0;
        while (true)
        {
            TimeSpan delay = reconnectPolicy.GetDelay(retryAttempt);
            if (delay > TimeSpan.Zero)
            {
                await delayAsync(delay, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                RecordReconnectAttempt();
                await replaceAsync(serialOptions, cancellationToken).ConfigureAwait(false);
                RecordSuccessfulRecovery(
                    timeProvider.GetUtcNow(),
                    timeProvider.GetElapsedTime(
                        startedTimestamp,
                        timeProvider.GetTimestamp()));
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                RecordReconnectFailure();
                retryAttempt++;
            }
        }
    }

    private static Func<TimeSpan, CancellationToken, Task> CreateDelay(
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return (delay, cancellationToken) =>
            Task.Delay(delay, timeProvider, cancellationToken);
    }

    private void RecordRecoveryStarted(DateTimeOffset startedAtUtc)
    {
        lock (statisticsLock)
        {
            lastRecoveryStartedAtUtc = startedAtUtc;
        }
    }

    private void RecordReconnectAttempt()
    {
        lock (statisticsLock)
        {
            reconnectAttemptCount++;
        }
    }

    private void RecordReconnectFailure()
    {
        lock (statisticsLock)
        {
            reconnectFailureCount++;
        }
    }

    private void RecordSuccessfulRecovery(
        DateTimeOffset completedAtUtc,
        TimeSpan duration)
    {
        lock (statisticsLock)
        {
            successfulRecoveryCount++;
            lastRecoveryCompletedAtUtc = completedAtUtc;
            lastRecoveryDuration = duration;
        }
    }
}
