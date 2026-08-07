using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;

namespace Hase.Scpi.Kel103.Hosting;

internal sealed class Kel103PassiveHealthMonitor
{
    internal static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(5);

    private readonly Func<EndpointConnectionState> getConnectionState;
    private readonly Func<CancellationToken, Task> probeAsync;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;

    public Kel103PassiveHealthMonitor(
        Kel103PublishedAttachment attachment,
        TimeProvider timeProvider)
        : this(
            GetConnectionStateAccessor(attachment),
            GetProbe(attachment),
            CreateDelay(timeProvider))
    {
    }

    internal Kel103PassiveHealthMonitor(
        Func<EndpointConnectionState> getConnectionState,
        Func<CancellationToken, Task> probeAsync,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        this.getConnectionState = getConnectionState
            ?? throw new ArgumentNullException(nameof(getConnectionState));
        this.probeAsync = probeAsync
            ?? throw new ArgumentNullException(nameof(probeAsync));
        this.delayAsync = delayAsync
            ?? throw new ArgumentNullException(nameof(delayAsync));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await delayAsync(ProbeInterval, cancellationToken).ConfigureAwait(false);

            if (getConnectionState() != EndpointConnectionState.Ready)
            {
                continue;
            }

            try
            {
                await probeAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // The serialized connection slot projects a sanitized Faulted
                // state. Recovery supervision owns all subsequent action.
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

    private static Func<EndpointConnectionState> GetConnectionStateAccessor(
        Kel103PublishedAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return () => attachment.RuntimeEndpoint.ConnectionStatus.State;
    }

    private static Func<CancellationToken, Task> GetProbe(
        Kel103PublishedAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return attachment.ProbeHealthAsync;
    }
}
