namespace Hase.ProtocolExplorer.Transport;

/// <summary>
/// Represents a transport capable of exchanging raw protocol bytes.
///
/// The transport is intentionally unaware of protocol message types.
/// It simply transfers a request byte sequence and returns the
/// corresponding response byte sequence.
///
/// Real implementations may communicate over USB, TCP/IP, BLE,
/// MQTT or any other medium.
/// </summary>
internal interface IProtocolTransport
{
    Task<byte[]> SendAsync(
        byte[] request,
        CancellationToken cancellationToken = default);
}
