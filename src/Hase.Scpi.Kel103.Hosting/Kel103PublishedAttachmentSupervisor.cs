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
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly SemaphoreSlim statusChanged = new(0);

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
            CreateDelay(timeProvider))
    {
    }

    internal Kel103PublishedAttachmentSupervisor(
        RuntimeEndpoint runtimeEndpoint,
        Func<SerialTransportOptions, CancellationToken, Task> replaceAsync,
        SerialTransportOptions serialOptions,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
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

    private async Task WaitForFaultAsync(CancellationToken cancellationToken)
    {
        while (runtimeEndpoint.ConnectionStatus.State != EndpointConnectionState.Faulted)
        {
            await statusChanged.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
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
                await replaceAsync(serialOptions, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
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
}
