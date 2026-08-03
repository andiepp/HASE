using Hase.Scpi;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed class Kel103SerialScpiByteStreamFactory
{
    private const int RequiredBaudRate = 115200;
    private readonly ISerialByteStreamFactory serialByteStreamFactory;

    public Kel103SerialScpiByteStreamFactory(ISerialByteStreamFactory serialByteStreamFactory)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
    }

    public async ValueTask<IScpiByteStream> OpenAsync(
        SerialTransportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateCharacterizedSettings(options);
        cancellationToken.ThrowIfCancellationRequested();

        ISerialByteStream serialByteStream = await serialByteStreamFactory
            .OpenAsync(options, cancellationToken)
            .ConfigureAwait(false);

        return new Kel103SerialScpiByteStream(serialByteStream);
    }

    private static void ValidateCharacterizedSettings(SerialTransportOptions options)
    {
        if (options.BaudRate != RequiredBaudRate
            || options.DataBits != 8
            || options.Parity != SerialParity.None
            || options.StopBits != SerialStopBits.One
            || options.Handshake != SerialHandshake.None)
        {
            throw new ArgumentException(
                "The KEL-103 serial adapter requires the characterized 115200 baud, 8-N-1, no-flow-control settings.",
                nameof(options));
        }
    }
}
