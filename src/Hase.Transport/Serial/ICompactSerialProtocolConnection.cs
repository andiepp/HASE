namespace Hase.Transport.Serial;

/// <summary>
/// Exchanges Compact Serial Protocol frames over one owned serial byte stream.
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