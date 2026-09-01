using System.IO;
using System.Runtime.CompilerServices;
using Hase.CompactProtocol;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.Configuration;
using Hase.DesktopHost.Hosting;
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
using Hase.Simulation.Runtime.ByteBuffer;

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
    private readonly DesktopRuntimeHostEndpointProviderRegistry
        endpointProviders;

    private RuntimeEndpointAttachmentHost? attachmentHost;
    private RuntimeHostNorthboundSnapshotComposition? composition;
    private RuntimeHostPrivateNetworkDeployment? deployment;
    private RuntimeHostDevelopmentLoopbackDeployment? developmentDeployment;
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

    /// <summary>
    /// Composes the host over the endpoint providers this application
    /// ships.
    /// </summary>
    public ProductionPrivateNetworkRuntimeHostBackend(
        DesktopRuntimeHostStartupConfiguration configuration)
        : this(
            configuration,
            CreateDefaultEndpointProviders())
    {
    }

    /// <summary>
    /// Composes the host over an explicitly supplied provider registry.
    /// </summary>
    /// <remarks>
    /// A composition root that ships other endpoint families registers
    /// them here; this backend names no family of its own.
    /// </remarks>
    public ProductionPrivateNetworkRuntimeHostBackend(
        DesktopRuntimeHostStartupConfiguration configuration,
        DesktopRuntimeHostEndpointProviderRegistry endpointProviders)
    {
        this.configuration =
            configuration
            ?? throw new ArgumentNullException(nameof(configuration));
        this.endpointProviders =
            endpointProviders
            ?? throw new ArgumentNullException(nameof(endpointProviders));
    }

    /// <summary>
    /// Composes the endpoint providers this application ships: the two
    /// endpoint kinds that carry no instrument knowledge.
    /// </summary>
    /// <remarks>
    /// This application names no instrument. A composition root that ships
    /// instruments registers their providers alongside these.
    /// </remarks>
    public static DesktopRuntimeHostEndpointProviderRegistry
        CreateDefaultEndpointProviders() =>
        new(
            [
                new DesktopRuntimeHostNativeNetworkEndpointProvider(),
                new DesktopRuntimeHostCompactSerialEndpointProvider()
            ]);

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
            || deployment is not null
            || developmentDeployment is not null)
        {
            throw new InvalidOperationException(
                "The production runtime host is already started.");
        }

        if (configuration.DevelopmentProfile is not null)
        {
            if (configuration.MediaConfiguration is not null)
            {
                throw new InvalidOperationException(
                    "The development loopback profile does not support "
                    + "Runtime Host media.");
            }

            if (configuration.RemoteDiagnosticsEnabled)
            {
                throw new InvalidOperationException(
                    "The development loopback profile does not support "
                    + "remote diagnostics.");
            }
        }

        DesktopRuntimeHostProductionConfigurationPlan productionPlan =
            DesktopRuntimeHostProductionConfigurationPlan.Create(
                configuration,
                configuration.InstallationProfile is null
                    ? GetRuntimeIdentityFilePath()
                    : configuration.InstallationProfile.IdentityFilePath,
                RuntimeHostId);
        DesktopRuntimeHostEndpointCompositionProfile? endpointComposition =
            productionPlan.EndpointComposition;
        CompactEndpointDefinition compactDefinition =
            ArduinoUnoCompactDefinitionFactory.Create();
        CompactEndpointDefinition legacyCompactDefinition =
            ArduinoUnoCompactDefinitionFactory.CreateLegacy();
        CompactEndpointDefinition lightCompactDefinition =
            ArduinoUnoLightCompactDefinitionFactory.Create();
        var definitionRepository =
            new InMemoryCompactEndpointDefinitionRepository(
                [
                    legacyCompactDefinition,
                    compactDefinition,
                    lightCompactDefinition
                ]);

        // Resolution runs before any runtime resource exists, so a
        // provider's preflight fails exactly where it failed before the
        // families moved behind the registry.
        DesktopRuntimeHostEndpointResolution endpointResolution =
            endpointComposition is null
                ? DesktopRuntimeHostEndpointResolution.Empty
                : await endpointProviders.ResolveAsync(
                    new DesktopRuntimeHostEndpointProviderContext(
                        endpointComposition,
                        definitionRepository),
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

            Func<RuntimeContext, IEndpointAttachmentService>
                providerAttachmentServiceFactory =
                    runtimeContext =>
                        endpointProviders.CreateAttachmentService(
                            runtimeContext,
                            endpointResolution);

            attachmentHost = configuration.IncludeByteBufferSimulation
                ? RuntimeEndpointAttachmentHost
                    .CreateNativeNetworkCompactSerialAndInProcess(
                        new ProtocolNativeEndpointBootstrapper(),
                        new ProtocolRuntimeEndpointSynchronizer(
                            new EndpointDescriptorCompatibilityValidator()),
                        definitionRepository,
                        new DefaultRuntimeEndpointReconnectPolicy(),
                        providerAttachmentServiceFactory,
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
                        providerAttachmentServiceFactory,
                        MaximumPayloadLength,
                        CompactEndpointHealthProbeOptions.Default,
                        diagnostics: session.Publisher);

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
                        endpointResolution);
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

            if (configuration.DevelopmentProfile
                is DesktopRuntimeHostDevelopmentProfile developmentProfile)
            {
                developmentDeployment =
                    RuntimeHostDevelopmentLoopbackDeployment.Create(
                        new LoopbackGrpcBinding(
                            developmentProfile.LoopbackAddress,
                            developmentProfile.Port),
                        composition.SnapshotProvider,
                        composition.PropertyService,
                        composition.CommandService,
                        composition.ObservationService);

                await developmentDeployment.Application.StartAsync(
                    cancellationToken);

                PublishDevelopmentProfileActive(
                    session.Publisher,
                    developmentProfile);
            }
            else
            {
                deployment =
                    await RuntimeHostPrivateNetworkDeployment.CreateAsync(
                        configuration.DeploymentOptions!,
                        composition.SnapshotProvider,
                        composition.PropertyService,
                        composition.CommandService,
                        composition.ObservationService,
                        cancellationToken: cancellationToken,
                        diagnosticProjectionService: projectionService,
                        authorizationPolicy: authorizationPolicy,
                        mediaSessionOwner: mediaSessionOwner);

                await deployment.Application.StartAsync(cancellationToken);
            }

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

        if (developmentDeployment is not null)
        {
            await developmentDeployment.Application.StopAsync(
                cancellationToken);
        }

        await DisposeStartedResourcesAsync();
    }

    private static void PublishDevelopmentProfileActive(
        RuntimeDiagnosticPublisher diagnostics,
        DesktopRuntimeHostDevelopmentProfile developmentProfile)
    {
        diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () =>
                new RuntimeDiagnosticEvent(
                    RuntimeDiagnosticLevel.Operational,
                    RuntimeDiagnosticCategory.RuntimeConnection,
                    "DevelopmentLoopbackHostingActive",
                    RuntimeDiagnosticSeverity.Warning,
                    RuntimeHostId.Value,
                    outcome: RuntimeDiagnosticOutcome.Succeeded,
                    details:
                        new Dictionary<string, string>(
                            StringComparer.Ordinal)
                        {
                            ["Profile"] = "DevelopmentLoopback",
                            ["Binding"] =
                                developmentProfile.BindingDisplay,
                            ["Security"] =
                                "None - loopback only, no TLS, "
                                + "no client certificates"
                        }));
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

    /// <summary>
    /// Binds each resolved attachment to this host's inventory.
    /// </summary>
    private static IReadOnlyList<DesktopRuntimeHostEndpointRefreshTarget>
        CreateConfiguredEndpointTargets(
            RuntimeEndpointAttachmentHost host,
            DesktopRuntimeHostEndpointResolution endpointResolution)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(endpointResolution);

        var targets =
            new List<DesktopRuntimeHostEndpointRefreshTarget>();

        foreach (DesktopRuntimeHostEndpointAttachment attachment
            in endpointResolution.Attachments)
        {
            targets.Add(
                new DesktopRuntimeHostEndpointRefreshTarget(
                    attachment.EndpointId,
                    attachment.EndpointKind,
                    token => attachment.AttachAsync(
                        host.AttachmentInventory,
                        token)));
        }

        return targets;
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
        RuntimeHostDevelopmentLoopbackDeployment?
            developmentDeploymentToDispose =
                developmentDeployment;
        RuntimeHostNorthboundSnapshotComposition? compositionToDispose =
            composition;
        RuntimeEndpointAttachmentHost? attachmentHostToDispose =
            attachmentHost;
        RuntimeHostMediaApplicationCoordinator? mediaCoordinatorToDispose =
            mediaCoordinator;

        endpointRefreshCoordinator = null;
        endpointRefreshTargets = [];
        deployment = null;
        developmentDeployment = null;
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

        if (developmentDeploymentToDispose is not null)
        {
            await developmentDeploymentToDispose.DisposeAsync();
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
