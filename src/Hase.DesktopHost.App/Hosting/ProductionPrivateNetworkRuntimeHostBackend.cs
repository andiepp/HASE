using System.IO;
using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.App.Physical;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Northbound;
using Hase.Runtime.Remote.Grpc.Hosting;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Tcp;

namespace Hase.DesktopHost.App.Hosting;

public sealed class ProductionPrivateNetworkRuntimeHostBackend
    : IDesktopRuntimeHostBackend,
      IDesktopRuntimeHostInventorySource
{
    private const int NativeTcpPort = 5000;
    private const int MaximumPayloadLength = 4096;
    private const int CompactBaudRate = 115200;
    private const ushort ArduinoVendorId = 0x2341;
    private const ushort ArduinoUnoProductId = 0x0043;

    private static readonly TimeSpan CompactVerificationTimeout =
        TimeSpan.FromSeconds(3);

    public static readonly RuntimeHostId RuntimeHostId =
        new("hase-desktop-runtime-host");

    private readonly DesktopRuntimeHostStartupConfiguration configuration;

    private RuntimeEndpointAttachmentHost? attachmentHost;
    private RuntimeHostNorthboundSnapshotComposition? composition;
    private RuntimeHostPrivateNetworkDeployment? deployment;

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
                        endpoint.Descriptor.Metadata?.DisplayName
                            ?? endpoint.EndpointId.Value,
                        endpoint.ConnectionStatus.State.ToString(),
                        endpoint.Generation.ToString()))
            .ToArray();
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

        try
        {
            CompactEndpointDefinition compactDefinition =
                ArduinoUnoCompactDefinitionFactory.Create();
            var definitionRepository =
                new InMemoryCompactEndpointDefinitionRepository(
                    [compactDefinition]);

            attachmentHost =
                RuntimeEndpointAttachmentHost
                    .CreateNativeNetworkAndCompactSerial(
                        new ProtocolNativeEndpointBootstrapper(),
                        new ProtocolRuntimeEndpointSynchronizer(
                            new EndpointDescriptorCompatibilityValidator()),
                        definitionRepository,
                        new DefaultRuntimeEndpointReconnectPolicy(),
                        MaximumPayloadLength,
                        CompactEndpointHealthProbeOptions.Default);

            composition =
                await RuntimeHostNorthboundSnapshotComposition
                    .CreateFileBackedAsync(
                        attachmentHost.AttachmentInventory,
                        GetRuntimeIdentityFilePath(),
                        RuntimeHostId);

            await AttachNativeEndpointAsync(
                attachmentHost,
                configuration.Esp32Host);
            await AttachCompactEndpointAsync(
                attachmentHost,
                definitionRepository);

            PublishedRuntimeHostSnapshot snapshot =
                composition.SnapshotProvider.Capture();

            if (snapshot.Endpoints.Count != 2)
            {
                throw new InvalidDataException(
                    "The desktop runtime host requires exactly two published "
                    + "physical endpoints.");
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

    private static async Task AttachNativeEndpointAsync(
        RuntimeEndpointAttachmentHost host,
        string endpointHost)
    {
        var request =
            new EndpointAttachmentRequest(
                NetworkEndpointConnectionDefinition.FromConfiguration(
                    new TcpTransportOptions(
                        endpointHost,
                        NativeTcpPort),
                    PhysicalEndpointIdentities.Esp32EndpointId),
                EndpointProvidedDescriptorSource.Instance);

        await host.AttachmentInventory.AttachAsync(
            request);
    }

    private static async Task AttachCompactEndpointAsync(
        RuntimeEndpointAttachmentHost host,
        ICompactEndpointDefinitionRepository definitionRepository)
    {
        var descriptorRepository =
            new CompactEndpointDescriptorRepositoryAdapter(
                definitionRepository);
        var candidateFilter =
            new UsbSerialEndpointMetadataFilter(
                vendorId: ArduinoVendorId,
                productId: ArduinoUnoProductId);

        UsbSerialEndpointDiscoveryService discoveryService =
            WindowsUsbSerialEndpointDiscovery.Create(
                descriptorRepository,
                candidateFilter);
        var discoveryOptions =
            new UsbSerialEndpointDiscoveryOptions(
                CompactBaudRate,
                CompactVerificationTimeout);

        UsbSerialEndpointDiscoveryResult discoveryResult =
            await discoveryService.DiscoverAsync(
                discoveryOptions);

        if (discoveryResult.VerifiedEndpoints.Count != 1)
        {
            throw new InvalidOperationException(
                "The desktop runtime host requires exactly one "
                + "authoritatively verified Arduino Uno endpoint after "
                + "VID/PID filtering.");
        }

        VerifiedUsbSerialEndpoint selectedEndpoint =
            discoveryResult.VerifiedEndpoints[0];
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
