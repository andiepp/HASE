namespace Hase.Transport;

/// <summary>
/// Provides independent framed send and receive operations for a transport
/// connection.
/// </summary>
/// <remarks>
/// This is an optional transport capability.
///
/// Each byte array represents one complete transport payload. Framing remains
/// the responsibility of the transport implementation.
///
/// A duplex protocol session may send and receive concurrently, but must have
/// only one active receive operation. The protocol receive pump is expected to
/// be the sole receive caller.
///
/// <see cref="ITransportConnection.ExchangeAsync"/> must not be used
/// concurrently while a connection is operating through this duplex
/// capability.
/// </remarks>
public interface ITransportDuplexConnection
    : ITransportConnection
{
    /// <summary>
    /// Sends one complete transport payload.
    /// </summary>
    /// <param name="payload">
    /// The complete payload to send.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the send operation.
    /// </param>
    Task SendAsync(
        byte[] payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives one complete transport payload.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the receive operation.
    /// </param>
    /// <returns>
    /// The complete received payload.
    /// </returns>
    Task<byte[]> ReceiveAsync(
        CancellationToken cancellationToken = default);
}