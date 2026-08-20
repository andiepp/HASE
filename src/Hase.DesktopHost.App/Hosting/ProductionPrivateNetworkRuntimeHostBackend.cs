using System.IO;
using System.Runtime.CompilerServices;
using Hase.CompactProtocol;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.Configuration;
using Hase.DesktopHost.App.Physical;
using Hase.DesktopHost.App.Media;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Media;
using Hase.Runtime.Northbound;
using Hase.Runtime.Remote.Grpc.Adapter;
using Hase.Runtime.Remote.Grpc.Hosting;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Transport.Discovery;
using Hase.Scpi.Kel103;
using Hase.Simulation.Runtime.ByteBuffer;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using Hase.Transport.Tcp;

namespace Hase.DesktopHost.App.Hosting;

public sealed class ProductionPrivateNetworkRuntimeHostBackend
    : IDesktopRuntimeHostBackend,
      IDesktopRuntimeHostEndpointRefresher,
      IDesktopRuntimeHostInventorySource,
      IDesktopRuntimeHostOperator,
      IDesktopRuntimeHostEventSource,
      IDesktopRuntimeDiagnosticSource
{
    private const int MaximumPayloadLength = 4096;

    public static readonly RuntimeHostId RuntimeHostId =
        new("hase-desktop-runtime-host");

    private readonly DesktopRuntimeHostStartupConfiguration configuration;
    private readonly Func<RuntimeContext, IEndpointAttachmentService>
        kel103AttachmentServiceProvider;

    private RuntimeEndpointAttachmentHost? attachmentHost;
    private RuntimeHostNorthboundSnapshotComposition? composition;
    private RuntimeHostPrivateNetworkDeployment? deployment;
    private DesktopRuntimeHostOperator? runtimeOperator;
    private DesktopRuntimeDiagnosticSession? diagnosticSession;
    private IRuntimeHostMediaWebBoundary? mediaBoundary;
    private IRuntimeHostMediaInventoryWebBoundary? mediaInventoryBoundary;
    private RuntimeHostMediaApplicationCoordinator? mediaCoordinator;
    private RuntimeHostMediaSessionOwner? mediaSessionOwner;
    private DesktopRuntimeHostEndpointRefreshCoordinator?
        endpointRefreshCoordinator;
    private IReadOnlyList<DesktopRuntimeHostEndpointRefreshTarget>
        endpointRefreshTargets = [];

    public ProductionPrivateNetworkRuntimeHostBackend(
        DesktopRuntimeHostStartupConfiguration configuration)
        : this(
            configuration,
            runtimeContext =>
                new DesktopRuntimeHostKel103AttachmentService(
                    new DesktopRuntimeHostKel103AttachmentFactory(
                        runtimeContext,
                        new SystemIoPortsSerialByteStreamFactory())))
    {
    }

    internal ProductionPrivateNetworkRuntimeHostBackend(
        DesktopRuntimeHostStartupConfiguration configuration,
        Func<RuntimeContext, IEndpointAttachmentService>
            kel103AttachmentServiceProvider)
    {
        this.configuration =
            configuration
            ?? throw new ArgumentNullException(nameof(configuration));
        this.kel103AttachmentServiceProvider =
            kel103AttachmentServiceProvider
            ?? throw new ArgumentNullException(nameof(kel103AttachmentServiceProvider));
    }

    public void ConfigureMediaBoundary(
        IRuntimeHostMediaWebBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        if (configuration.MediaConfiguration is null)
        {
            throw new InvalidOperationException(
                "A media boundary requires explicit local media configuration.");
        }
        if (mediaBoundary is not null || deployment is not null)
        {
            throw new InvalidOperationException(
                "The Runtime Host media boundary is already configured or started.");
        }
        mediaBoundary = boundary;
    }

    public void ConfigureMediaBoundaries(
        IRuntimeHostMediaWebBoundary captureBoundary,
        IRuntimeHostMediaInventoryWebBoundary inventoryBoundary)
    {
        ArgumentNullException.ThrowIfNull(captureBoundary);
        ArgumentNullException.ThrowIfNull(inventoryBoundary);
        if (configuration.MediaConfiguration is not
            { DynamicInventoryEnabled: true })
        {
            throw new InvalidOperationException(
                "Dynamic media boundaries require dynamic local media configuration.");
        }
        if (mediaBoundary is not null || mediaInventoryBoundary is not null ||
            deployment is not null)
        {
            throw new InvalidOperationException(
                "The Runtime Host media boundaries are already configured or started.");
        }
        mediaBoundary = captureBoundary;
        mediaInventoryBoundary = inventoryBoundary;
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

        try
        {
            RuntimeHostAuthorizationPolicy? authorizationPolicy =
                configuration.InstallationProfile?.AuthorizationPolicyFilePath
                    is string authorizationPolicyFilePath
                    ? await RuntimeHostAuthorizationPolicyFile.LoadAsync(
                        authorizationPolicyFilePath,
                        cancellationToken)
                    : null;
            var session =
                new DesktopRuntimeDiagnosticSession(
                    configuration.MaximumDiagnosticLevel);

            diagnosticSession =
                session;

            if (configuration.MediaConfiguration is not null)
            {
                IRuntimeHostMediaWebBoundary configuredBoundary =
                    mediaBoundary ?? throw new InvalidOperationException(
                        "Configured Runtime Host media requires its WPF capture boundary.");
                DesktopRuntimeHostMediaConfiguration mediaConfiguration =
                    configuration.MediaConfiguration;
                if (mediaConfiguration.DynamicInventoryEnabled)
                {
                    IRuntimeHostMediaInventoryWebBoundary inventoryBoundary =
                        mediaInventoryBoundary ?? throw new InvalidOperationException(
                            "Dynamic media requires its inventory WebView boundary.");
                    mediaSessionOwner = new RuntimeHostMediaSessionOwner(
                        [],
                        configuredBoundary,
                        allowEmptySources: true);
                    var reconciler = new RuntimeHostMediaInventoryReconciler(
                        mediaConfiguration.Sources,
                        mediaConfiguration.IdentityKey!);
                    mediaCoordinator = new RuntimeHostMediaApplicationCoordinator(
                        configuredBoundary,
                        mediaSessionOwner,
                        inventoryBoundary,
                        reconciler,
                        session.Publisher);
                    await mediaCoordinator.InitializeInventoryAsync(
                        cancellationToken);
                }
                else
                {
                    mediaSessionOwner = new RuntimeHostMediaSessionOwner(
                        mediaConfiguration.Sources,
                        configuredBoundary);
                    mediaCoordinator = new RuntimeHostMediaApplicationCoordinator(
                        configuredBoundary,
                        mediaSessionOwner,
                        session.Publisher);
                }
            }

            CompactEndpointDefinition compactDefinition =
                ArduinoUnoCompactDefinitionFactory.Create();
            CompactEndpointDefinition legacyCompactDefinition =
                ArduinoUnoCompactDefinitionFactory.CreateLegacy();
            var definitionRepository =
                new InMemoryCompactEndpointDefinitionRepository(
                    [legacyCompactDefinition, compactDefinition]);

            bool hasKel103Endpoints = kel103Plans.Count > 0;

            if (configuration.IncludeByteBufferSimulation)
            {
                attachmentHost = hasKel103Endpoints
                    ? RuntimeEndpointAttachmentHost
                        .CreateNativeNetworkCompactSerialAndInProcess(
                            new ProtocolNativeEndpointBootstrapper(),
                            new ProtocolRuntimeEndpointSynchronizer(
                                new EndpointDescriptorCompatibilityValidator()),
                            definitionRepository,
                            new DefaultRuntimeEndpointReconnectPolicy(),
                            kel103AttachmentServiceProvider,
                            MaximumPayloadLength,
                            CompactEndpointHealthProbeOptions.Default,
                            diagnostics: session.Publisher)
                    : RuntimeEndpointAttachmentHost
                        .CreateNativeNetworkCompactSerialAndInProcess(
                            new ProtocolNativeEndpointBootstrapper(),
                            new ProtocolRuntimeEndpointSynchronizer(
                                new EndpointDescriptorCompatibilityValidator()),
                            definitionRepository,
                            new DefaultRuntimeEndpointReconnectPolicy(),
                            MaximumPayloadLength,
                            CompactEndpointHealthProbeOptions.Default,
                            diagnostics: session.Publisher);
            }
            else
            {
                attachmentHost = hasKel103Endpoints
                    ? RuntimeEndpointAttachmentHost
                        .CreateNativeNetworkAndCompactSerial(
                            new ProtocolNativeEndpointBootstrapper(),
                            new ProtocolRuntimeEndpointSynchronizer(
                                new EndpointDescriptorCompatibilityValidator()),
                            definitionRepository,
                            new DefaultRuntimeEndpointReconnectPolicy(),
                            kel103AttachmentServiceProvider,
                            MaximumPayloadLength,
                            CompactEndpointHealthProbeOptions.Default,
                            diagnostics: session.Publisher)
                    : RuntimeEndpointAttachmentHost
                        .CreateNativeNetworkAndCompactSerial(
                            new ProtocolNativeEndpointBootstrapper(),
                            new ProtocolRuntimeEndpointSynchronizer(
                                new EndpointDescriptorCompatibilityValidator()),
                            definitionRepository,
                            new DefaultRuntimeEndpointReconnectPolicy(),
                            MaximumPayloadLength,
                            CompactEndpointHealthProbeOptions.Default,
                            diagnostics: session.Publisher);
            }

            RuntimeEndpointAttachmentHost currentAttachmentHost =
                attachmentHost
                ?? throw new InvalidOperationException(
                    "The production attachment host was not created.");

            composition =
                await RuntimeHostNorthboundSnapshotComposition
                    .CreateFileBackedAsync(
                        currentAttachmentHost.AttachmentInventory,
                        productionPlan.IdentityFilePath,
                        productionPlan.ConfiguredRuntimeHostId,
                        diagnostics:
                            currentAttachmentHost
                                .RuntimeContext
                                .Diagnostics);

            runtimeOperator =
                new DesktopRuntimeHostOperator(
                    composition.PropertyService,
                    composition.CommandService);

            RuntimeHostDiagnosticProjectionService? projectionService = null;
            if (configuration.RemoteDiagnosticsEnabled)
            {
                projectionService = session.AttachProjection(
                    composition.IdentityResolution.RuntimeHostId,
                    new RuntimeHostDiagnosticProjectionPolicy(
                        isEnabled: true,
                        maximumLevel:
                            configuration.RemoteDiagnosticsMaximumLevel));
            }

            var endpointStartup =
                new DesktopRuntimeHostEndpointStartupCoordinator(
                    session.Publisher);
            IReadOnlyList<DesktopRuntimeHostEndpointRefreshTarget>
                configuredEndpointTargets =
                    CreateConfiguredEndpointTargets(
                        currentAttachmentHost,
                        endpointComposition,
                        definitionRepository,
                        kel103Plans);
            int successfullyAttachedEndpointCount = 0;

            foreach (DesktopRuntimeHostEndpointRefreshTarget target
                in configuredEndpointTargets)
            {
                if (await endpointStartup.TryAttachAsync(
                        target.EndpointId,
                        target.EndpointKind,
                        target.AttachAsync,
                        cancellationToken))
                {
                    successfullyAttachedEndpointCount++;
                }
            }

            if (configuration.IncludeByteBufferSimulation)
            {
                await AttachByteBufferSimulationAsync(
                    currentAttachmentHost,
                    cancellationToken);
                successfullyAttachedEndpointCount++;
            }

            PublishedRuntimeHostSnapshot snapshot =
                composition.SnapshotProvider.Capture();

            if (snapshot.Endpoints.Count != successfullyAttachedEndpointCount)
            {
                throw new InvalidDataException(
                    "The published endpoint count does not match the "
                    + "successful startup attachment count.");
            }

            deployment =
                await RuntimeHostPrivateNetworkDeployment.CreateAsync(
                    configuration.DeploymentOptions,
                    composition.SnapshotProvider,
                    composition.PropertyService,
                    composition.CommandService,
                    composition.ObservationService,
                    cancellationToken: cancellationToken,
                    diagnosticProjectionService: projectionService,
                    authorizationPolicy: authorizationPolicy,
                    mediaSessionOwner: mediaSessionOwner);

            await deployment.Application.StartAsync(cancellationToken);

            endpointRefreshCoordinator =
                new DesktopRuntimeHostEndpointRefreshCoordinator(
                    endpointId =>
                        currentAttachmentHost
                            .AttachmentInventory
                            .Find(new EndpointId(endpointId))
                        is not null,
                    session.Publisher);
            endpointRefreshTargets = configuredEndpointTargets;
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

    public Task RefreshEndpointsAsync(
        CancellationToken cancellationToken = default)
    {
        DesktopRuntimeHostEndpointRefreshCoordinator coordinator =
            endpointRefreshCoordinator
            ?? throw new InvalidOperationException(
                "The desktop runtime host is not running.");

        return coordinator.RefreshAsync(
            endpointRefreshTargets,
            cancellationToken);
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

    private static IReadOnlyList<DesktopRuntimeHostEndpointRefreshTarget>
        CreateConfiguredEndpointTargets(
            RuntimeEndpointAttachmentHost host,
            DesktopRuntimeHostEndpointCompositionProfile endpointComposition,
            ICompactEndpointDefinitionRepository definitionRepository,
            IReadOnlyList<DesktopRuntimeHostKel103EndpointPlan> kel103Plans)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(endpointComposition);
        ArgumentNullException.ThrowIfNull(definitionRepository);
        ArgumentNullException.ThrowIfNull(kel103Plans);

        if (kel103Plans.Count
            != endpointComposition.Kel103SerialEndpoints.Count)
        {
            throw new InvalidDataException(
                "The KEL-103 endpoint plans do not match the configured "
                + "endpoint count.");
        }

        var targets =
            new List<DesktopRuntimeHostEndpointRefreshTarget>();

        foreach (DesktopRuntimeHostNativeNetworkEndpointProfile endpoint
            in endpointComposition.NativeNetworkEndpoints)
        {
            targets.Add(
                new DesktopRuntimeHostEndpointRefreshTarget(
                    endpoint.ExpectedEndpointId,
                    "NativeNetwork",
                    token => AttachNativeEndpointAsync(
                        host,
                        endpoint,
                        token)));
        }

        foreach (DesktopRuntimeHostCompactSerialEndpointProfile endpoint
            in endpointComposition.CompactSerialEndpoints)
        {
            targets.Add(
                new DesktopRuntimeHostEndpointRefreshTarget(
                    endpoint.ExpectedEndpointId,
                    "CompactSerial",
                    token => AttachCompactEndpointAsync(
                        host,
                        definitionRepository,
                        endpoint,
                        token)));
        }

        for (int index = 0; index < kel103Plans.Count; index++)
        {
            DesktopRuntimeHostKel103SerialEndpointProfile endpoint =
                endpointComposition.Kel103SerialEndpoints[index];
            DesktopRuntimeHostKel103EndpointPlan plan =
                kel103Plans[index];

            targets.Add(
                new DesktopRuntimeHostEndpointRefreshTarget(
                    endpoint.ExpectedEndpointId,
                    "Kel103Serial",
                    token => AttachKel103EndpointAsync(
                        host,
                        endpoint,
                        plan,
                        token)));
        }

        return targets;
    }

    private static async Task AttachNativeEndpointAsync(
        RuntimeEndpointAttachmentHost host,
        DesktopRuntimeHostNativeNetworkEndpointProfile endpoint,
        CancellationToken cancellationToken)
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
            request,
            cancellationToken);
    }

    private static async Task AttachCompactEndpointAsync(
        RuntimeEndpointAttachmentHost host,
        ICompactEndpointDefinitionRepository definitionRepository,
        DesktopRuntimeHostCompactSerialEndpointProfile endpoint,
        CancellationToken cancellationToken)
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
                discoveryOptions,
                cancellationToken);

        if (discoveryResult.VerifiedEndpoints.Count == 0)
        {
            throw new DesktopRuntimeHostEndpointUnavailableException(
                "NoVerifiedCandidate");
        }

        if (discoveryResult.VerifiedEndpoints.Count > 1)
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
            request,
            cancellationToken);
    }

    private static async Task AttachKel103EndpointAsync(
        RuntimeEndpointAttachmentHost host,
        DesktopRuntimeHostKel103SerialEndpointProfile endpoint,
        DesktopRuntimeHostKel103EndpointPlan plan,
        CancellationToken cancellationToken)
    {
        if (new EndpointId(endpoint.ExpectedEndpointId) != plan.ExpectedEndpointId)
        {
            throw new InvalidDataException(
                "A KEL-103 endpoint profile does not match its preflight plan.");
        }

        var request = new EndpointAttachmentRequest(
            new DesktopRuntimeHostKel103ConnectionDefinition(
                plan.ExpectedEndpointId,
                plan.Definition,
                new SerialTransportOptions(
                    endpoint.SerialPort,
                    endpoint.BaudRate)),
            HostRepositoryDescriptorSource.Instance);

        await host.AttachmentInventory.AttachAsync(
            request,
            cancellationToken);
    }

    private static async Task AttachByteBufferSimulationAsync(
        RuntimeEndpointAttachmentHost host,
        CancellationToken cancellationToken)
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
            request,
            cancellationToken);
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
        RuntimeHostMediaApplicationCoordinator? mediaCoordinatorToDispose =
            mediaCoordinator;

        endpointRefreshCoordinator = null;
        endpointRefreshTargets = [];
        deployment = null;
        composition = null;
        attachmentHost = null;
        runtimeOperator = null;
        diagnosticSession = null;
        mediaCoordinator = null;
        mediaSessionOwner = null;
        mediaBoundary = null;
        mediaInventoryBoundary = null;

        if (deploymentToDispose is not null)
        {
            await deploymentToDispose.DisposeAsync();
        }

        if (mediaCoordinatorToDispose is not null)
        {
            await mediaCoordinatorToDispose.DisposeAsync();
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
