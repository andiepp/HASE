using System.Threading;
using System.Threading.Tasks;

namespace Hase.Transport;

/// <summary>
/// Represents a bidirectional byte transport between a HASE runtime
/// and a remote endpoint.
///
/// The transport is intentionally protocol-independent.
/// It only exchanges byte sequences.
/// </summary>
public interface ITransportConnection
{
    /// <summary>
    /// Occurs when the locally observable lifecycle state changes.
    /// </summary>
    event EventHandler<TransportConnectionStateChangedEventArgs>?
        StateChanged;

    /// <summary>
    /// Gets the locally observable lifecycle state of the connection.
    /// </summary>
    TransportConnectionState State
    {
        get;
    }

    /// <summary>
    /// Sends a request and waits for the corresponding response.
    /// </summary>
    Task<byte[]> ExchangeAsync(
        byte[] request,
        CancellationToken cancellationToken = default);
}