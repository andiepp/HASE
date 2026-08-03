using Hase.Transport.Serial;

namespace Hase.Scpi.Serial;

/// <summary>
/// Opens serial transport streams and exposes them through the SCPI byte-stream boundary.
/// </summary>
public sealed class SerialScpiByteStreamFactory
{
    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public SerialScpiByteStreamFactory(ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public async ValueTask<SerialScpiByteStream> OpenAsync(
        SerialTransportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        ISerialByteStream serialByteStream = await serialByteStreamFactory
            .OpenAsync(options, cancellationToken)
            .ConfigureAwait(false);

        return new SerialScpiByteStream(serialByteStream);
    }
}
