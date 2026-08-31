using Hase.Transport.Serial;

namespace Hase.Mcnf.Serial;

/// <summary>
/// Opens serial transport streams and exposes them through the MCNF
/// byte-stream boundary. An optional settle delay covers nodes that reset
/// when the port opens, such as auto-resetting Arduino boards driven by the
/// DTR line.
/// </summary>
public sealed class SerialMcnfByteStreamFactory
{
    private readonly ISerialByteStreamFactory serialByteStreamFactory;
    private readonly TimeProvider timeProvider;

    public SerialMcnfByteStreamFactory(
        ISerialByteStreamFactory serialByteStreamFactory,
        TimeProvider? timeProvider = null)
    {
        this.serialByteStreamFactory = serialByteStreamFactory
            ?? throw new ArgumentNullException(nameof(serialByteStreamFactory));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<SerialMcnfByteStream> OpenAsync(
        SerialTransportOptions options,
        TimeSpan settleDelay,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (settleDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settleDelay),
                settleDelay,
                "The MCNF serial settle delay must not be negative.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        ISerialByteStream serialByteStream = await serialByteStreamFactory
            .OpenAsync(options, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            if (settleDelay > TimeSpan.Zero)
            {
                await Task
                    .Delay(settleDelay, timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }

            return new SerialMcnfByteStream(serialByteStream);
        }
        catch
        {
            await serialByteStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
