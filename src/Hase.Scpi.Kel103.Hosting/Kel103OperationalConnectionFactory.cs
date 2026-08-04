using System.Globalization;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Diagnostics;
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
        => await OpenAsync(
            endpointId,
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition,
            serialOptions,
            cancellationToken).ConfigureAwait(false);

    public async Task<Kel103OperationalConnection> OpenAsync(
        EndpointId endpointId,
        EndpointDescriptorDefinition definition,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpointId);
        ArgumentNullException.ThrowIfNull(definition);
        DescriptorReference reference = SupportedReference(definition);
        return await OpenCoreAsync(
            () => runtimeContext.CreateEndpoint(
                definition.Materialize(endpointId)),
            endpointId,
            reference,
            definition,
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
            runtimeEndpoint.Descriptor.Id,
            RuntimeReference(runtimeEndpoint),
            RuntimeDefinition(runtimeEndpoint),
            serialOptions,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Kel103OperationalConnection> OpenCoreAsync(
        Func<RuntimeEndpoint> createRuntimeEndpoint,
        EndpointId endpointId,
        DescriptorReference reference,
        EndpointDescriptorDefinition definition,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serialOptions);
        ValidateSerialProfile(serialOptions);

        var operation = new RuntimeDiagnosticOperation(
            runtimeContext.Diagnostics,
            RuntimeDiagnosticCategory.RuntimeSynchronization,
            "InstrumentSynchronizationStarted",
            "InstrumentSynchronizationCompleted",
            "InstrumentSynchronizationFailed",
            endpointId.Value,
            attachmentGeneration: null,
            direction: null,
            details: SynchronizationDetails(reference, definition),
            timeProvider: timeProvider);

        return await operation.RunAsync(
            token => OpenConnectionCoreAsync(
                createRuntimeEndpoint,
                serialOptions,
                token),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Kel103OperationalConnection> OpenConnectionCoreAsync(
        Func<RuntimeEndpoint> createRuntimeEndpoint,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken)
    {
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

    private static IReadOnlyDictionary<string, string> SynchronizationDetails(
        DescriptorReference reference,
        EndpointDescriptorDefinition definition) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DefinitionId"] = reference.Id.Value,
            ["DefinitionVersion"] =
                reference.Version.ToString(CultureInfo.InvariantCulture),
            ["PropertyCount"] =
                definition.Instruments
                    .Sum(instrument => instrument.Interface.Properties.Count)
                    .ToString(CultureInfo.InvariantCulture)
        };

    private static DescriptorReference SupportedReference(
        EndpointDescriptorDefinition definition)
    {
        if (ReferenceEquals(definition, Kel103ReadOnlyMeasurementDefinition.EndpointDefinition))
        {
            return Kel103ReadOnlyMeasurementDefinition.Reference;
        }

        if (ReferenceEquals(definition, Kel103OperatingStateDefinition.EndpointDefinition))
        {
            return Kel103OperatingStateDefinition.Reference;
        }

        if (ReferenceEquals(definition, Kel103ControlledSetpointDefinition.EndpointDefinition))
        {
            return Kel103ControlledSetpointDefinition.Reference;
        }

        throw new InvalidDataException(
            "The supplied endpoint definition is not an exact supported KEL-103 definition.");
    }

    private static DescriptorReference RuntimeReference(RuntimeEndpoint endpoint) =>
        endpoint.Instruments.Single().Commands.Count > 0
            ? Kel103ControlledSetpointDefinition.Reference
            : endpoint.Instruments.Single().Properties.Count >
                Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Instruments.Single()
                    .Interface.Properties.Count
                ? Kel103OperatingStateDefinition.Reference
                : Kel103ReadOnlyMeasurementDefinition.Reference;

    private static EndpointDescriptorDefinition RuntimeDefinition(RuntimeEndpoint endpoint) =>
        endpoint.Instruments.Single().Commands.Count > 0
            ? Kel103ControlledSetpointDefinition.EndpointDefinition
            : endpoint.Instruments.Single().Properties.Count >
                Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Instruments.Single()
                    .Interface.Properties.Count
                ? Kel103OperatingStateDefinition.EndpointDefinition
                : Kel103ReadOnlyMeasurementDefinition.EndpointDefinition;

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
