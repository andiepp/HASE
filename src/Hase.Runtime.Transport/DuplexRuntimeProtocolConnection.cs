using Hase.Protocol;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Adapts a protocol duplex session to the runtime protocol-connection
/// abstraction.
/// </summary>
public sealed class DuplexRuntimeProtocolConnection
    : IRuntimeProtocolConnection,
      IRuntimeProtocolNotificationSource,
      ITransportExchangeTraceSource
{
    private readonly ProtocolDuplexSession _session;

    /// <summary>
    /// Initializes the adapter.
    /// </summary>
    public DuplexRuntimeProtocolConnection(
        ProtocolDuplexSession session)
    {
        _session =
            session
            ?? throw new ArgumentNullException(
                nameof(session));
    }

    /// <summary>
    /// Gets the underlying duplex protocol session.
    /// </summary>
    public ProtocolDuplexSession Session =>
        _session;

    /// <inheritdoc />
    public Task<ProtocolMessage> SendAsync(
        ProtocolMessage request,
        CancellationToken cancellationToken = default)
    {
        return _session.SendAsync(
            request,
            cancellationToken);
    }

    /// <inheritdoc />
    public void SubscribeNotification(
        IProtocolNotificationObserver observer)
    {
        _session.SubscribeNotification(
            observer);
    }

    /// <inheritdoc />
    public void UnsubscribeNotification(
        IProtocolNotificationObserver observer)
    {
        _session.UnsubscribeNotification(
            observer);
    }

    /// <inheritdoc />
    public void SubscribeTrace(
        ITransportExchangeTraceObserver observer)
    {
        _session.SubscribeTrace(
            observer);
    }

    /// <inheritdoc />
    public void UnsubscribeTrace(
        ITransportExchangeTraceObserver observer)
    {
        _session.UnsubscribeTrace(
            observer);
    }
}