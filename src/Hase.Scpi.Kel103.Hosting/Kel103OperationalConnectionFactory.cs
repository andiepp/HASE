using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Scpi.Kel103.Runtime;
using Hase.Scpi.Serial;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting;

/// <summary>
/// Opens and synchronizes one staged KEL-103 runtime endpoint.
/// </summary>
public sealed class Kel103OperationalConnectionFactory
{
    private static readonly ScpiTextFramingOptions Framing = new(
        ScpiCommandTerminator.CarriageReturn,
        ScpiResponseTerminator.LineFeed,
        TimeSpan.FromSeconds(3),
        maximumResponseBytes: 512);

    private readonly RuntimeContext runtimeContext;
    private readonly SerialScpiByteStreamFactory streamFactory;
    private readonly TimeProvider timeProvider;

    public Kel103OperationalConnectionFactory(
        RuntimeContext runtimeContext,
        ISerialByteStreamFactory serialByteStreamFactory,
        TimeProvider? timeProvider = null)
    {
        this.runtimeContext = runtimeContext
            ?? throw new ArgumentNullException(nameof(runtimeContext));
        streamFactory = new SerialScpiByteStreamFactory(
            serialByteStreamFactory
                ?? throw new ArgumentNullException(nameof(serialByteStreamFactory)));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Kel103OperationalConnection> OpenAsync(
        EndpointId endpointId,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpointId);
        return await OpenCoreAsync(
            () => runtimeContext.CreateEndpoint(
                Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Materialize(endpointId)),
            serialOptions,
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<Kel103OperationalConnection> OpenForEndpointAsync(
        RuntimeEndpoint runtimeEndpoint,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeEndpoint);
        if (!ReferenceEquals(runtimeEndpoint.Context, runtimeContext))
        {
            throw new ArgumentException(
                "The runtime endpoint belongs to a different runtime context.",
                nameof(runtimeEndpoint));
        }

        return await OpenCoreAsync(
            () => runtimeEndpoint,
            serialOptions,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Kel103OperationalConnection> OpenCoreAsync(
        Func<RuntimeEndpoint> createRuntimeEndpoint,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serialOptions);
        ValidateSerialProfile(serialOptions);
        cancellationToken.ThrowIfCancellationRequested();

        IAsyncDisposable? owner = null;
        try
        {
            SerialScpiByteStream byteStream = await streamFactory
                .OpenAsync(serialOptions, cancellationToken)
                .ConfigureAwait(false);
            owner = byteStream;

            var textSession = new ScpiTextSession(byteStream, Framing);
            owner = textSession;

            var sessionAdapter = new Kel103ReadOnlySessionAdapter(textSession, timeProvider);
            owner = sessionAdapter;

            RuntimeEndpoint runtimeEndpoint = createRuntimeEndpoint();
            var runtimeAdapter = new Kel103RuntimeEndpointAdapter(
                sessionAdapter,
                runtimeEndpoint,
                timeProvider);
            owner = runtimeAdapter;

            await runtimeAdapter.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

            var connection = new Kel103OperationalConnection(
                runtimeAdapter,
                new Kel103EndpointAttachmentPropertyOperations(runtimeAdapter, timeProvider));
            owner = null;
            return connection;
        }
        catch (Exception primaryFailure)
        {
            if (owner is not null)
            {
                try
                {
                    await owner.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(
                        "The KEL-103 connection attempt and its cleanup both failed.",
                        primaryFailure,
                        cleanupFailure);
                }
            }

            throw;
        }
    }

    private static void ValidateSerialProfile(SerialTransportOptions options)
    {
        if (options.BaudRate != 115200
            || options.DataBits != 8
            || options.Parity != SerialParity.None
            || options.StopBits != SerialStopBits.One
            || options.Handshake != SerialHandshake.None)
        {
            throw new ArgumentException(
                "The serial settings do not match the supported KEL-103 profile.",
                nameof(options));
        }
    }
}
