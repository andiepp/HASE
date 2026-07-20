namespace Hase.Transport.Serial;

/// <summary>
/// Opens the serial byte stream owned by one transport connection.
/// </summary>
internal interface ISerialByteStreamFactory
{
    /// <summary>
    /// Opens a serial byte stream using the supplied connection settings.
    /// </summary>
    ValueTask<ISerialByteStream> OpenAsync(
        SerialTransportOptions options,
        CancellationToken cancellationToken = default);
}