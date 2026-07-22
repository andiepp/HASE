using Hase.Transport;

namespace Hase.CompactProtocol;

/// <summary>
/// Exchanges Compact Serial Protocol frames and exposes decoded unsolicited
/// event notifications over one owned serial byte stream.
/// </summary>
internal interface ICompactSerialProtocolConnection
    : IAsyncDisposable
{
    /// <summary>
    /// Occurs when the locally observable connection state changes.
    /// </summary>
    event EventHandler<TransportConnectionStateChangedEventArgs>?
        StateChanged;

    /// <summary>
    /// Occurs when one valid unsolicited compact event notification is
    /// received.
    /// </summary>
    /// <remarks>
    /// Implementations that do not produce unsolicited notifications may use
    /// the default no-op accessors. The production compact serial connection
    /// provides the active implementation.
    /// </remarks>
    event Action<CompactEventNotification>?
        EventNotificationReceived
    {
        add
        {
        }

        remove
        {
        }
    }

    /// <summary>
    /// Gets the locally observable lifecycle state.
    /// </summary>
    TransportConnectionState State
    {
        get;
    }

    /// <summary>
    /// Sends one request frame and returns its correlated response frame.
    /// </summary>
    Task<CompactSerialFrame> ExchangeAsync(
        CompactSerialFrame request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the connection as faulted and unavailable for further use.
    /// </summary>
    void Invalidate();
}