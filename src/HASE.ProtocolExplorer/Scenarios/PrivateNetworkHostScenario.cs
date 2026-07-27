using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Northbound;
using Hase.Runtime.Remote.Grpc.Hosting;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Runs the controlled private-network runtime host with explicitly attached
/// physical native-network and compact-serial endpoints.
/// </summary>
internal sealed class PrivateNetworkHostScenario
    : IParameterizedScenario
{
    private const int NativeTcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    private const int CompactBaudRate =
        115200;

    private static readonly TimeSpan CompactVerificationTimeout =
        TimeSpan.FromSeconds(
            3);

    private const ushort ArduinoVendorId =
        0x2341;

    private const ushort ArduinoUnoProductId =
        0x0043;

    private static readonly RuntimeHostId RuntimeHostId =
        new(
            "protocol-explorer-private-network-validation");

    public string Name =>
        "private-network-host";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        PrivateNetworkHostArguments parsedArguments =
            ParseArguments(
                arguments);

        ExecuteAsync(
                parsedArguments)
            .GetAwaiter()
            .GetResult();
    }

    internal static PrivateNetworkHostArguments ParseArguments(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count != 2
            || string.IsNullOrWhiteSpace(
                arguments[1]))
        {
            throw new ArgumentException(
                "The private-network host requires exactly one desktop "
                + "configuration file and one ESP32 host name or IP address.",
                nameof(arguments));
        }

        return new PrivateNetworkHostArguments(
            new PrivateNetworkConfigurationFileArguments(
                arguments[0]),
            arguments[1]);
    }

    private static async Task ExecuteAsync(
        PrivateNetworkHostArguments arguments)
    {
        RuntimeHostPrivateNetworkDeploymentOptions options =
            await RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(
                arguments.Configuration.ConfigurationFilePath);

        CompactEndpointDefinition compactDefinition =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreateCompactDefinition();
        var definitionRepository =
            new InMemoryCompactEndpointDefinitionRepository(
                [
                    compactDefinition
                ]);

        await using RuntimeEndpointAttachmentHost attachmentHost =
            RuntimeEndpointAttachmentHost
                .CreateNativeNetworkAndCompactSerial(
                    new ProtocolNativeEndpointBootstrapper(),
                    new ProtocolRuntimeEndpointSynchronizer(
                        new EndpointDescriptorCompatibilityValidator()),
                    definitionRepository,
                    new DefaultRuntimeEndpointReconnectPolicy(),
                    MaximumPayloadLength,
                    CompactEndpointHealthProbeOptions.Default);

        await using RuntimeHostNorthboundSnapshotComposition composition =
            await RuntimeHostNorthboundSnapshotComposition
                .CreateFileBackedAsync(
                    attachmentHost.AttachmentInventory,
                    Path.Combine(
                        Path.GetTempPath(),
                        "hase-protocol-explorer",
                        "private-network-runtime-host-identity.json"),
                    RuntimeHostId);

        await AttachNativeEndpointAsync(
            attachmentHost,
            arguments.Esp32Host);
        await AttachCompactEndpointAsync(
            attachmentHost,
            definitionRepository);

        PublishedRuntimeHostSnapshot snapshot =
            composition.SnapshotProvider.Capture();

        if (snapshot.Endpoints.Count != 2)
        {
            throw new InvalidDataException(
                "The private-network runtime host requires exactly two "
                + "published physical endpoints.");
        }

        await using RuntimeHostPrivateNetworkDeployment deployment =
            await RuntimeHostPrivateNetworkDeployment.CreateAsync(
                options,
                composition.SnapshotProvider,
                composition.PropertyService,
                composition.CommandService,
                composition.ObservationService);

        var shutdown =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
        ConsoleCancelEventHandler cancelHandler =
            (_, eventArguments) =>
            {
                eventArguments.Cancel =
                    true;
                shutdown.TrySetResult();
            };
        Console.CancelKeyPress +=
            cancelHandler;

        try
        {
            await deployment.Application.StartAsync();

            WriteRunningHost(
                snapshot);

            await shutdown.Task;
        }
        finally
        {
            Console.CancelKeyPress -=
                cancelHandler;
            await deployment.Application.StopAsync();
        }
    }

    private static async Task AttachNativeEndpointAsync(
        RuntimeEndpointAttachmentHost attachmentHost,
        string endpointHost)
    {
        var request =
            new EndpointAttachmentRequest(
                NetworkEndpointConnectionDefinition.FromConfiguration(
                    new TcpTransportOptions(
                        endpointHost,
                        NativeTcpPort),
                    PhysicalEnvironmentEndpointDescriptorFactory.EndpointId),
                EndpointProvidedDescriptorSource.Instance);

        await attachmentHost.AttachmentInventory.AttachAsync(
            request);
    }

    private static async Task AttachCompactEndpointAsync(
        RuntimeEndpointAttachmentHost attachmentHost,
        ICompactEndpointDefinitionRepository definitionRepository)
    {
        var descriptorRepository =
            new CompactEndpointDescriptorRepositoryAdapter(
                definitionRepository);
        var candidateFilter =
            new UsbSerialEndpointMetadataFilter(
                vendorId:
                    ArduinoVendorId,
                productId:
                    ArduinoUnoProductId);
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
                "The private-network runtime host requires exactly one "
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

        await attachmentHost.AttachmentInventory.AttachAsync(
            request);
    }

    private static void WriteRunningHost(
        PublishedRuntimeHostSnapshot snapshot)
    {
        Console.WriteLine(
            "Private-network runtime host");
        Console.WriteLine(
            "============================");
        Console.WriteLine();
        Console.WriteLine(
            "Transport             : HTTPS / HTTP/2 gRPC");
        Console.WriteLine(
            "Client authentication : Mutual TLS");
        Console.WriteLine(
            "Listener configuration: External and withheld");
        Console.WriteLine(
            "Physical reachability : External and withheld");
        Console.WriteLine(
            "Attachment selection  : Explicit and authoritative");
        Console.WriteLine(
            $"Published endpoints    : {snapshot.Endpoints.Count}");

        foreach (PublishedRuntimeEndpointSnapshot endpoint
            in snapshot.Endpoints)
        {
            Console.WriteLine(
                $"  {endpoint.EndpointId.Value}");
        }

        Console.WriteLine();
        Console.WriteLine(
            "Press Ctrl+C to stop.");
    }
}

internal sealed record PrivateNetworkHostArguments(
    PrivateNetworkConfigurationFileArguments Configuration,
    string Esp32Host);
