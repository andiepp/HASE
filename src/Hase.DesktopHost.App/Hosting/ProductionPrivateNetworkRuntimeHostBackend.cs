using System.IO;
using System.Runtime.CompilerServices;
using Hase.CompactProtocol;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.Configuration;
using Hase.DesktopHost.App.Physical;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Northbound;
using Hase.Runtime.Remote.Grpc.Hosting;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Transport.Discovery;
using Hase.Scpi.Kel103;
using Hase.Simulation.Runtime.ByteBuffer;
using Hase.Transport.Discovery;
using Hase.Transport.Tcp;

namespace Hase.DesktopHost.App.Hosting;

public sealed class ProductionPrivateNetworkRuntimeHostBackend
    : IDesktopRuntimeHostBackend,
      IDesktopRuntimeHostInventorySource,
      IDesktopRuntimeHostOperator,
      IDesktopRuntimeHostEventSource,
      IDesktopRuntimeDiagnosticSource
{
    private const int MaximumPayloadLength = 4096;

    public static readonly RuntimeHostId RuntimeHostId =
        new("hase-desktop-runtime-host");

    private readonly DesktopRuntimeHostStartupConfiguration configuration;

    private RuntimeEndpointAttachmentHost? attachmentHost;
    private RuntimeHostNorthboundSnapshotComposition? composition;
    private RuntimeHostPrivateNetworkDeployment? deployment;
    private DesktopRuntimeHostOperator? runtimeOperator;
    private DesktopRuntimeDiagnosticSession? diagnosticSession;

    public ProductionPrivateNetworkRuntimeHostBackend(
        DesktopRuntimeHostStartupConfiguration configuration)
    {
        this.configuration =
            configuration
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    public IReadOnlyList<DesktopRuntimeEndpointSnapshot> Capture()
    {
        RuntimeHostNorthboundSnapshotComposition? currentComposition =
            composition;

        if (currentComposition is null)
        {
            return [];
        }

        PublishedRuntimeHostSnapshot snapshot =
            currentComposition.SnapshotProvider.Capture();

        return snapshot.Endpoints
            .Select(
                endpoint =>
                    new DesktopRuntimeEndpointSnapshot(
                        endpoint.EndpointId.Value,
                        endpoint.Descriptor.Metadata.DisplayName
                            ?? endpoint.EndpointId.Value,
                        endpoint.ConnectionStatus.State.ToString(),
                        endpoint.Generation.ToString())
                    {
                        Description =
                            endpoint.Descriptor.Metadata.Description,
                        Instruments =
                            endpoint.Descriptor.Instruments
                                .Select(
                                    instrument =>
                                        new DesktopRuntimeInstrumentSnapshot(
                                            instrument.Id.Value,
                                            instrument.Name,
                                            instrument.Kind.Name,
                                            instrument.Metadata.Manufacturer,
                                            instrument.Metadata.Model,
                                            instrument.Metadata.SerialNumber,
                                            instrument.Metadata.FirmwareVersion,
                                            instrument.Metadata.HardwareRevision,
                                            instrument.Metadata.Description)
                                        {
                                            Properties =
                                                instrument.Interface.Properties
                                                    .Select(
                                                        property =>
                                                            CaptureProperty(
                                                                currentComposition,
                                                                endpoint,
                                                                instrument,
                                                                property))
                                                    .ToArray(),
                                            Commands =
                                                instrument.Interface.Commands
                                                    .Select(
                                                        command =>
                                                            new DesktopRuntimeCommandSnapshot(
                                                                new RuntimeHostCommandTarget(
                                                                    endpoint.EndpointId,
                                                                    endpoint.Generation,
                                                                    instrument.Id,
                                                                    command.Path),
                                                                command.Path.ToString(),
                                                                command.DisplayName,
                                                                command.Description,
                                                                endpoint.ConnectionStatus.State
                                                                    == EndpointConnectionState.Ready,
                                                                command))
                                                    .ToArray(),
                                            Events =
                                                instrument.Interface.Events
                                                    .Select(
                                                        eventDescriptor =>
                                                            new DesktopRuntimeEventSnapshot(
                                                                eventDescriptor))
                                                    .ToArray()
                                        })
                                .ToArray()
                    })
            .ToArray();
    }

    private static DesktopRuntimePropertySnapshot CaptureProperty(
        RuntimeHostNorthboundSnapshotComposition currentComposition,
        PublishedRuntimeEndpointSnapshot endpoint,
        Hase.Core.Domain.Instruments.InstrumentDescriptor instrument,
        Hase.Core.Domain.Properties.PropertyDescriptor property)
    {
        var target =
            new RuntimeHostPropertyTarget(
                endpoint.EndpointId,
                endpoint.Generation,
                instrument.Id,
                property.Id);
        RuntimeHostCachedPropertyResult result =
            currentComposition.PropertyService.GetCached(
                target);

        if (!result.IsSuccess
            || result.Snapshot?.CurrentValue is null)
        {
            return new DesktopRuntimePropertySnapshot(
                target,
                property.Id.Value,
                property.DisplayName,
                property.Path.ToString(),
                property.AccessMode.ToString(),
                "Unknown",
                "Unknown",
                string.Empty,
                IsKnown: false,
                GetDataKind(
                    property.Data),
                CanRead(
                    property.AccessMode),
                CanWrite(
                    property.AccessMode),
                BooleanValue: null,
                endpoint.ConnectionStatus.State
                    == EndpointConnectionState.Ready,
                property,
                CurrentValue: null);
        }

        Hase.Core.Domain.Properties.PropertyValue currentValue =
            result.Snapshot.CurrentValue;

        return new DesktopRuntimePropertySnapshot(
            target,
            property.Id.Value,
            property.DisplayName,
            property.Path.ToString(),
            property.AccessMode.ToString(),
            FormatPropertyValue(
                currentValue.Value),
            currentValue.Quality.ToString(),
            currentValue.TimestampUtc.ToString(
                "O",
                System.Globalization.CultureInfo.InvariantCulture),
            IsKnown: true,
            GetDataKind(
                property.Data),
            CanRead(
                property.AccessMode),
            CanWrite(
                property.AccessMode),
            currentValue.Value is bool booleanValue
                ? booleanValue
                : null,
            endpoint.ConnectionStatus.State
                == EndpointConnectionState.Ready,
            property,
            currentValue.Value);
    }

    private static DesktopRuntimePropertyDataKind GetDataKind(
        DataDescriptor descriptor) =>
        descriptor switch
        {
            BooleanDataDescriptor =>
                DesktopRuntimePropertyDataKind.Boolean,
            NumericDataDescriptor =>
                DesktopRuntimePropertyDataKind.Numeric,
            StringDataDescriptor =>
                DesktopRuntimePropertyDataKind.String,
            ByteArrayDataDescriptor =>
                DesktopRuntimePropertyDataKind.ByteArray,
            _ =>
                DesktopRuntimePropertyDataKind.Unknown
        };

    private static bool CanWrite(
        Hase.Core.Domain.Properties.PropertyAccessMode accessMode) =>
        accessMode.HasFlag(
            Hase.Core.Domain.Properties.PropertyAccessMode.Write);

    private static bool CanRead(
        Hase.Core.Domain.Properties.PropertyAccessMode accessMode) =>
        accessMode.HasFlag(
            Hase.Core.Domain.Properties.PropertyAccessMode.Read);

    private static string FormatPropertyValue(
        object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is ByteArrayValue byteArrayValue)
        {
            return Convert.ToHexString(
                byteArrayValue.AsSpan());
        }

        return value is IFormattable formattable
            ? formattable.ToString(
                format: null,
                System.Globalization.CultureInfo.InvariantCulture)
                ?? string.Empty
            : value.ToString()
                ?? string.Empty;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (attachmentHost is not null
            || composition is not null
            || deployment is not null)
        {
            throw new InvalidOperationException(
                "The production runtime host is already started.");
        }

        DesktopRuntimeHostProductionConfigurationPlan productionPlan =
            DesktopRuntimeHostProductionConfigurationPlan.Create(
                configuration,
                configuration.InstallationProfile is null
                    ? GetRuntimeIdentityFilePath()
                    : configuration.InstallationProfile.IdentityFilePath,
                RuntimeHostId);
        DesktopRuntimeHostEndpointCompositionProfile endpointComposition =
            productionPlan.EndpointComposition;
        IReadOnlyList<DesktopRuntimeHostKel103EndpointPlan> kel103Plans =
            await DesktopRuntimeHostKel103DefinitionPreflight.ResolveAllAsync(
                endpointComposition.Kel103SerialEndpoints,
                new Kel103DefinitionRepository(),
                cancellationToken);

        if (kel103Plans.Count > 0)
        {
            throw new NotSupportedException(
                "Configured KEL-103 endpoint attachment is not enabled by the production runtime host yet.");
        }

        try
        {
            var session =
                new DesktopRuntimeDiagnosticSession(
                    configuration.MaximumDiagnosticLevel);

            diagnosticSession =
                session;

            CompactEndpointDefinition compactDefinition =
                ArduinoUnoCompactDefinitionFactory.Create();
            CompactEndpointDefinition legacyCompactDefinition =
                ArduinoUnoCompactDefinitionFactory.CreateLegacy();
            var definitionRepository =
                new InMemoryCompactEndpointDefinitionRepository(
                    [legacyCompactDefinition, compactDefinition]);

            attachmentHost =
                configuration.IncludeByteBufferSimulation
                    ? RuntimeEndpointAttachmentHost
                        .CreateNativeNetworkCompactSerialAndInProcess(
                            new ProtocolNativeEndpointBootstrapper(),
                            new ProtocolRuntimeEndpointSynchronizer(
                                new EndpointDescriptorCompatibilityValidator()),
                            definitionRepository,
                            new DefaultRuntimeEndpointReconnectPolicy(),
                            MaximumPayloadLength,
                            CompactEndpointHealthProbeOptions.Default,
                            diagnostics:
                                session.Publisher)
                    : RuntimeEndpointAttachmentHost
                        .CreateNativeNetworkAndCompactSerial(
                            new ProtocolNativeEndpointBootstrapper(),
                            new ProtocolRuntimeEndpointSynchronizer(
                                new EndpointDescriptorCompatibilityValidator()),
                            definitionRepository,
                            new DefaultRuntimeEndpointReconnectPolicy(),
                            MaximumPayloadLength,
                            CompactEndpointHealthProbeOptions.Default,
                            diagnostics:
                                session.Publisher);

            composition =
                await RuntimeHostNorthboundSnapshotComposition
                    .CreateFileBackedAsync(
                        attachmentHost.AttachmentInventory,
                        productionPlan.IdentityFilePath,
                        productionPlan.ConfiguredRuntimeHostId,
                        diagnostics:
                            attachmentHost
                                .RuntimeContext
                                .Diagnostics);

            runtimeOperator =
                new DesktopRuntimeHostOperator(
                    composition.PropertyService,
                    composition.CommandService);

            foreach (
                DesktopRuntimeHostNativeNetworkEndpointProfile nativeEndpoint
                in endpointComposition.NativeNetworkEndpoints)
            {
                await AttachNativeEndpointAsync(
                    attachmentHost,
                    nativeEndpoint);
            }

            foreach (
                DesktopRuntimeHostCompactSerialEndpointProfile compactEndpoint
                in endpointComposition.CompactSerialEndpoints)
            {
                await AttachCompactEndpointAsync(
                    attachmentHost,
                    definitionRepository,
                    compactEndpoint);
            }

            if (configuration.IncludeByteBufferSimulation)
            {
                await AttachByteBufferSimulationAsync(
                    attachmentHost);
            }

            PublishedRuntimeHostSnapshot snapshot =
                composition.SnapshotProvider.Capture();

            int expectedEndpointCount =
                productionPlan.ExpectedPublishedEndpointCount;

            if (snapshot.Endpoints.Count != expectedEndpointCount)
            {
                throw new InvalidDataException(
                    $"The desktop runtime host requires exactly "
                    + $"{expectedEndpointCount} published endpoints for the "
                    + "selected startup mode.");
            }

            deployment =
                await RuntimeHostPrivateNetworkDeployment.CreateAsync(
                    configuration.DeploymentOptions,
                    composition.SnapshotProvider,
                    composition.PropertyService,
                    composition.CommandService,
                    composition.ObservationService);

            await deployment.Application.StartAsync(cancellationToken);
        }
        catch
        {
            await DisposeStartedResourcesAsync();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (deployment is not null)
        {
            await deployment.Application.StopAsync(cancellationToken);
        }

        await DisposeStartedResourcesAsync();
    }

    public Task<RuntimeHostPropertyOperationResult> WritePropertyAsync(
        RuntimeHostPropertyTarget target,
        object? requestedValue,
        CancellationToken cancellationToken = default)
    {
        DesktopRuntimeHostOperator currentOperator =
            runtimeOperator
            ?? throw new InvalidOperationException(
                "The desktop runtime host is not running.");

        return currentOperator.WritePropertyAsync(
            target,
            requestedValue,
            cancellationToken);
    }

    public Task<RuntimeHostPropertyOperationResult> ReadPropertyAsync(
        RuntimeHostPropertyTarget target,
        CancellationToken cancellationToken = default)
    {
        DesktopRuntimeHostOperator currentOperator =
            runtimeOperator
            ?? throw new InvalidOperationException(
                "The desktop runtime host is not running.");

        return currentOperator.ReadPropertyAsync(
            target,
            cancellationToken);
    }

    public Task<RuntimeHostCommandOperationResult> ExecuteCommandAsync(
        RuntimeHostCommandTarget target,
        object? argument,
        CancellationToken cancellationToken = default)
    {
        DesktopRuntimeHostOperator currentOperator =
            runtimeOperator
            ?? throw new InvalidOperationException(
                "The desktop runtime host is not running.");

        return currentOperator.ExecuteCommandAsync(
            target,
            argument,
            cancellationToken);
    }

    public async IAsyncEnumerable<DesktopRuntimeEventOccurrence>
        ObserveEventsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken =
                default)
    {
        RuntimeHostNorthboundSnapshotComposition currentComposition =
            composition
            ?? throw new InvalidOperationException(
                "The desktop runtime host is not running.");

        await using RuntimeHostObservationSubscription subscription =
            await currentComposition.ObservationService.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions(),
                cancellationToken);

        await foreach (
            RuntimeHostObservation observation
            in subscription.ReadAllAsync(
                cancellationToken))
        {
            if (observation.Payload
                is not RuntimeHostEventOccurredObservationPayload)
            {
                continue;
            }

            PublishedRuntimeHostSnapshot snapshot =
                currentComposition.SnapshotProvider.Capture();
            Hase.Core.Domain.Events.EventDescriptor? descriptor =
                DesktopRuntimeEventDescriptorResolver.Resolve(
                    snapshot,
                    observation);

            yield return DesktopRuntimeEventOccurrenceProjector.Project(
                observation,
                descriptor);
        }
    }

    public IReadOnlyList<RuntimeDiagnosticRecord> CaptureDiagnostics()
    {
        return diagnosticSession?.CaptureDiagnostics()
            ?? [];
    }

    public RuntimeDiagnosticLevel MaximumLevel =>
        diagnosticSession?.MaximumLevel
        ?? configuration.MaximumDiagnosticLevel;

    public void ClearDiagnostics()
    {
        diagnosticSession?.ClearDiagnostics();
    }

    private static async Task AttachNativeEndpointAsync(
        RuntimeEndpointAttachmentHost host,
        DesktopRuntimeHostNativeNetworkEndpointProfile endpoint)
    {
        var request =
            new EndpointAttachmentRequest(
                NetworkEndpointConnectionDefinition.FromConfiguration(
                    new TcpTransportOptions(
                        endpoint.Host,
                        endpoint.Port),
                    new EndpointId(
                        endpoint.ExpectedEndpointId)),
                EndpointProvidedDescriptorSource.Instance);

        await host.AttachmentInventory.AttachAsync(
            request);
    }

    private static async Task AttachCompactEndpointAsync(
        RuntimeEndpointAttachmentHost host,
        ICompactEndpointDefinitionRepository definitionRepository,
        DesktopRuntimeHostCompactSerialEndpointProfile endpoint)
    {
        var descriptorRepository =
            new CompactEndpointDescriptorRepositoryAdapter(
                definitionRepository);
        var candidateFilter =
            new UsbSerialEndpointMetadataFilter(
                vendorId: endpoint.VendorId,
                productId: endpoint.ProductId);

        UsbSerialEndpointDiscoveryService discoveryService =
            WindowsUsbSerialEndpointDiscovery.Create(
                descriptorRepository,
                candidateFilter);
        var discoveryOptions =
            new UsbSerialEndpointDiscoveryOptions(
                endpoint.BaudRate,
                endpoint.VerificationTimeout);

        UsbSerialEndpointDiscoveryResult discoveryResult =
            await discoveryService.DiscoverAsync(
                discoveryOptions);

        if (discoveryResult.VerifiedEndpoints.Count != 1)
        {
            throw new InvalidOperationException(
                "The desktop runtime host requires exactly one "
                + "authoritatively verified compact endpoint after "
                + "VID/PID filtering.");
        }

        VerifiedUsbSerialEndpoint selectedEndpoint =
            discoveryResult.VerifiedEndpoints[0];

        if (!selectedEndpoint.EndpointId.Equals(
                new EndpointId(
                    endpoint.ExpectedEndpointId)))
        {
            throw new InvalidDataException(
                "The verified compact endpoint identity does not match the configured expected identity.");
        }

        SerialEndpointConnectionDefinition connectionDefinition =
            SerialEndpointConnectionDefinition.FromVerifiedEndpoint(
                selectedEndpoint,
                discoveryOptions);
        var request =
            new EndpointAttachmentRequest(
                connectionDefinition,
                HostRepositoryDescriptorSource.Instance);

        await host.AttachmentInventory.AttachAsync(
            request);
    }

    private static async Task AttachByteBufferSimulationAsync(
        RuntimeEndpointAttachmentHost host)
    {
        var simulation =
            new ByteBufferSimulation();

        var request =
            new EndpointAttachmentRequest(
                new InProcessEndpointConnectionDefinition(
                    new EndpointDescriptor(
                        new EndpointId(
                            "simulation-byte-buffer-validation"),
                        [
                            ByteBufferDescriptorFactory.CreateDescriptor()
                        ]),
                    runtimeInstrument =>
                        new ByteBufferInstrumentExecutor(
                            simulation,
                            runtimeInstrument)),
                InProcessEndpointDescriptorSource.Instance);

        await host.AttachmentInventory.AttachAsync(
            request);
    }

    private static string GetRuntimeIdentityFilePath()
    {
        string directory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "HASE",
                "DesktopRuntimeHost");

        Directory.CreateDirectory(directory);

        return Path.Combine(
            directory,
            "runtime-host-identity.json");
    }

    private async Task DisposeStartedResourcesAsync()
    {
        RuntimeHostPrivateNetworkDeployment? deploymentToDispose =
            deployment;
        RuntimeHostNorthboundSnapshotComposition? compositionToDispose =
            composition;
        RuntimeEndpointAttachmentHost? attachmentHostToDispose =
            attachmentHost;

        deployment = null;
        composition = null;
        attachmentHost = null;
        runtimeOperator = null;
        diagnosticSession = null;

        if (deploymentToDispose is not null)
        {
            await deploymentToDispose.DisposeAsync();
        }

        if (compositionToDispose is not null)
        {
            await compositionToDispose.DisposeAsync();
        }

        if (attachmentHostToDispose is not null)
        {
            await attachmentHostToDispose.DisposeAsync();
        }
    }
}
