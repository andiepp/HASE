using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;

namespace Hase.Mcnf.RfLab.Hosting;

/// <summary>
/// Probes the published RF-Lab endpoint with the read-only MCNF
/// connectivity test, five seconds after the previous completed probe, only
/// while the endpoint is Ready, through the same serialized gate as every
/// other operation.
/// </summary>
internal sealed class RfLabPassiveHealthMonitor
{
    internal static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(5);

    private readonly Func<EndpointConnectionState> getConnectionState;
    private readonly Func<CancellationToken, Task> probeAsync;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;

    public RfLabPassiveHealthMonitor(
        RfLabPublishedAttachment attachment,
        TimeProvider timeProvider)
        : this(
            GetConnectionStateAccessor(attachment),
            GetProbe(attachment),
            CreateDelay(timeProvider))
    {
    }

    internal RfLabPassiveHealthMonitor(
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
        RfLabPublishedAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return () => attachment.RuntimeEndpoint.ConnectionStatus.State;
    }

    private static Func<CancellationToken, Task> GetProbe(
        RfLabPublishedAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return attachment.ProbeHealthAsync;
    }
}
