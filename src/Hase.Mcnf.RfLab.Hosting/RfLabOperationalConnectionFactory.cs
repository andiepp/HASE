using System.Globalization;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Mcnf.RfLab.Runtime;
using Hase.Mcnf.Serial;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

namespace Hase.Mcnf.RfLab.Hosting;

/// <summary>
/// Opens and synchronizes one staged RF-Lab runtime endpoint. The port is
/// opened with DTR and RTS asserted and the characterized settle delay
/// covers the node's open-triggered reset before the first exchange.
/// </summary>
public sealed class RfLabOperationalConnectionFactory
{
    private static readonly McnfFramingOptions Framing = new(
        TimeSpan.FromSeconds(5),
        RfLabProtocol.NodeBufferSize);

    /// <summary>
    /// The characterized reset settle time of the auto-resetting node after
    /// the port opens. The physical node needed more than 1.5 seconds before
    /// answering its first exchange; three seconds were verified against the
    /// device on 2026-08-31.
    /// </summary>
    public static readonly TimeSpan DefaultSettleDelay = TimeSpan.FromSeconds(3);

    private readonly RuntimeContext runtimeContext;
    private readonly SerialMcnfByteStreamFactory streamFactory;
    private readonly TimeSpan settleDelay;
    private readonly TimeProvider timeProvider;

    public RfLabOperationalConnectionFactory(
        RuntimeContext runtimeContext,
        ISerialByteStreamFactory serialByteStreamFactory,
        TimeSpan? settleDelay = null,
        TimeProvider? timeProvider = null)
    {
        this.runtimeContext = runtimeContext
            ?? throw new ArgumentNullException(nameof(runtimeContext));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        streamFactory = new SerialMcnfByteStreamFactory(
            serialByteStreamFactory
                ?? throw new ArgumentNullException(nameof(serialByteStreamFactory)),
            this.timeProvider);
        this.settleDelay = settleDelay ?? DefaultSettleDelay;
        if (this.settleDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(settleDelay));
        }
    }

    public async Task<RfLabOperationalConnection> OpenAsync(
        EndpointId endpointId,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken = default)
        => await OpenAsync(
            endpointId,
            RfLabReadOnlyDefinition.EndpointDefinition,
            serialOptions,
            cancellationToken).ConfigureAwait(false);

    public async Task<RfLabOperationalConnection> OpenAsync(
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

    internal async Task<RfLabOperationalConnection> OpenForEndpointAsync(
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

    private async Task<RfLabOperationalConnection> OpenCoreAsync(
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
                endpointId.Value,
                serialOptions,
                token),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<RfLabOperationalConnection> OpenConnectionCoreAsync(
        Func<RuntimeEndpoint> createRuntimeEndpoint,
        string endpointId,
        SerialTransportOptions serialOptions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IAsyncDisposable? owner = null;
        try
        {
            SerialMcnfByteStream byteStream = await streamFactory
                .OpenAsync(serialOptions, settleDelay, cancellationToken)
                .ConfigureAwait(false);
            owner = byteStream;

            var diagnosticObserver = new RfLabMcnfDiagnosticObserver(
                endpointId,
                runtimeContext.Diagnostics);
            var session = new McnfSession(
                byteStream,
                Framing,
                diagnosticObserver,
                timeProvider);
            owner = session;

            var sessionAdapter = new RfLabSessionAdapter(session, timeProvider);
            owner = sessionAdapter;

            RuntimeEndpoint runtimeEndpoint = createRuntimeEndpoint();
            var runtimeAdapter = new RfLabRuntimeEndpointAdapter(
                sessionAdapter,
                runtimeEndpoint,
                timeProvider);
            owner = runtimeAdapter;

            await runtimeAdapter.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

            var connection = new RfLabOperationalConnection(
                runtimeAdapter,
                new RfLabEndpointAttachmentPropertyOperations(runtimeAdapter, timeProvider),
                new RfLabEndpointAttachmentCommandOperations(runtimeAdapter, timeProvider));
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
                        "The RF-Lab connection attempt and its cleanup both failed.",
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
        if (ReferenceEquals(definition, RfLabReadOnlyDefinition.EndpointDefinition))
        {
            return RfLabReadOnlyDefinition.Reference;
        }

        if (ReferenceEquals(definition, RfLabControlledSignalDefinition.EndpointDefinition))
        {
            return RfLabControlledSignalDefinition.Reference;
        }

        throw new InvalidDataException(
            "The supplied endpoint definition is not an exact supported RF-Lab definition.");
    }

    private static DescriptorReference RuntimeReference(RuntimeEndpoint endpoint) =>
        endpoint.Instruments.Single().Commands.Count > 0
            ? RfLabControlledSignalDefinition.Reference
            : RfLabReadOnlyDefinition.Reference;

    private static EndpointDescriptorDefinition RuntimeDefinition(RuntimeEndpoint endpoint) =>
        endpoint.Instruments.Single().Commands.Count > 0
            ? RfLabControlledSignalDefinition.EndpointDefinition
            : RfLabReadOnlyDefinition.EndpointDefinition;

    private static void ValidateSerialProfile(SerialTransportOptions options)
    {
        if (options.BaudRate != RfLabProtocol.BaudRate
            || options.DataBits != 8
            || options.Parity != SerialParity.None
            || options.StopBits != SerialStopBits.One
            || options.Handshake != SerialHandshake.None
            || !options.AssertDataTerminalReady
            || !options.AssertRequestToSend)
        {
            throw new ArgumentException(
                "The serial settings do not match the supported RF-Lab profile.",
                nameof(options));
        }
    }
}
