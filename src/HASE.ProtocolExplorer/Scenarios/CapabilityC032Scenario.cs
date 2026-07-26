using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Northbound;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Tcp;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates an authenticated authoritative Property RPC against the physical
/// ESP32 while the runtime host retains endpoint lifecycle ownership.
/// </summary>
internal sealed class CapabilityC032Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    private static readonly RuntimeHostId RuntimeHostId =
        new(
            "protocol-explorer-authenticated-physical-validation");

    /// <inheritdoc />
    public string Name =>
        "c032";

    /// <inheritdoc />
    public void Execute(
        IReadOnlyList<string> arguments)
    {
        CapabilityC032Arguments parsedArguments =
            ParseArguments(
                arguments);

        ExecuteAsync(
                parsedArguments)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// Parses the strict C-032 command-line shape.
    /// </summary>
    internal static CapabilityC032Arguments ParseArguments(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count != 1)
        {
            throw new ArgumentException(
                "Capability C-032 requires exactly one ESP32 host name "
                + "or IP address.",
                nameof(arguments));
        }

        return new CapabilityC032Arguments(
            arguments[0]);
    }

    private static async Task ExecuteAsync(
        CapabilityC032Arguments arguments)
    {
        WriteHeader(
            arguments.EndpointHost);

        await using RuntimeEndpointAttachmentHost attachmentHost =
            RuntimeEndpointAttachmentHost.CreateNativeNetwork(
                new ProtocolNativeEndpointBootstrapper(),
                new ProtocolRuntimeEndpointSynchronizer(
                    new EndpointDescriptorCompatibilityValidator()),
                new DefaultRuntimeEndpointReconnectPolicy(),
                MaximumPayloadLength);

        NetworkEndpointConnectionDefinition connectionDefinition =
            NetworkEndpointConnectionDefinition.FromConfiguration(
                new TcpTransportOptions(
                    arguments.EndpointHost,
                    TcpPort),
                PhysicalEnvironmentEndpointDescriptorFactory.EndpointId);

        var request =
            new EndpointAttachmentRequest(
                connectionDefinition,
                EndpointProvidedDescriptorSource.Instance);

        RuntimeEndpointAttachmentInventoryEntry? entry =
            null;

        try
        {
            Console.WriteLine(
                "Attaching the physical native endpoint.");

            Console.WriteLine();

            entry =
                await attachmentHost.AttachmentInventory.AttachAsync(
                    request);

            await using RuntimeHostNorthboundSnapshotComposition
                northboundComposition =
                    await RuntimeHostNorthboundSnapshotComposition
                        .CreateFileBackedAsync(
                            attachmentHost.AttachmentInventory,
                            Path.Combine(
                                Path.GetTempPath(),
                                "hase-protocol-explorer",
                                "runtime-host-identity.json"),
                            RuntimeHostId);

            PublishedRuntimeEndpointSnapshot endpointSnapshot =
                GetPublishedEndpoint(
                    northboundComposition,
                    entry.EndpointId);

            var target =
                new RuntimeHostPropertyTarget(
                    endpointSnapshot.EndpointId,
                    endpointSnapshot.Generation,
                    PhysicalEnvironmentEndpointDescriptorFactory.InstrumentId,
                    PhysicalEnvironmentEndpointDescriptorFactory
                        .TemperaturePropertyId);

            DateTimeOffset validationTimeUtc =
                DateTimeOffset.UtcNow;

            await using CapabilityC032SecureHostComposition
                secureHostComposition =
                    await CapabilityC032SecureHostComposition.CreateAsync(
                        northboundComposition.SnapshotProvider,
                        northboundComposition.PropertyService,
                        validationTimeUtc);

            Uri secureAddress =
                await secureHostComposition.StartAsync();

            using CapabilityC032SecureGrpcClient client =
                CapabilityC032SecureGrpcClient.Create(
                    secureAddress,
                    secureHostComposition.AuthenticationComposition
                        .Certificates
                        .ClientCertificate,
                    secureHostComposition.AuthenticationComposition
                        .Certificates
                        .ServerCertificate);

            GrpcV1.PropertyOperationResult response =
                await client.ReadAuthoritativePropertyAsync(
                    target,
                    DateTime.UtcNow.AddSeconds(
                        10));

            if (response.Status
                != GrpcV1.PropertyOperationStatus.Success)
            {
                throw new InvalidDataException(
                    "The authenticated authoritative Property RPC failed "
                    + $"with status '{response.Status}'."
                    + (
                        response.HasDiagnostic
                            ? $" Diagnostic: {response.Diagnostic}"
                            : string.Empty));
            }

            if (response.ConfirmedValue?.Value is null)
            {
                throw new InvalidDataException(
                    "The authenticated authoritative Property RPC did not "
                    + "return a confirmed value.");
            }

            if (response.ConfirmedValue.Value.KindCase
                != GrpcV1.RemoteValue.KindOneofCase.NumericValue)
            {
                throw new InvalidDataException(
                    "The authenticated authoritative Property RPC did not "
                    + "return a numeric temperature.");
            }

            WriteResult(
                northboundComposition,
                endpointSnapshot,
                target,
                secureAddress,
                response);
        }
        finally
        {
            await DetachAsync(
                attachmentHost,
                entry);
        }
    }

    private static PublishedRuntimeEndpointSnapshot GetPublishedEndpoint(
        RuntimeHostNorthboundSnapshotComposition composition,
        EndpointId endpointId)
    {
        PublishedRuntimeEndpointSnapshot? endpointSnapshot =
            composition.InventorySnapshotProvider
                .List()
                .SingleOrDefault(
                    candidate =>
                        candidate.EndpointId
                        == endpointId);

        return endpointSnapshot
            ?? throw new InvalidDataException(
                $"The northbound inventory did not publish endpoint "
                + $"'{endpointId.Value}'.");
    }

    private static async Task DetachAsync(
        RuntimeEndpointAttachmentHost attachmentHost,
        RuntimeEndpointAttachmentInventoryEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        bool detached =
            await attachmentHost.AttachmentInventory.DetachAsync(
                entry.EndpointId);

        Console.WriteLine();

        Console.WriteLine(
            $"Orderly detachment     : {detached}");

        Console.WriteLine(
            $"Final connection state : "
            + $"{entry.RuntimeEndpoint.ConnectionStatus.State}");
    }

    private static void WriteHeader(
        string endpointHost)
    {
        const string title =
            "Capability C-032";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Validate an authenticated authoritative Property RPC against "
            + "the physical ESP32 through the mutual-TLS runtime host.");

        Console.WriteLine();

        Console.WriteLine(
            "Endpoint family       : Native Protocol Version 1");

        Console.WriteLine(
            $"Physical host         : {endpointHost}");

        Console.WriteLine(
            $"Physical port         : {TcpPort}");

        Console.WriteLine(
            "Remote transport      : HTTPS / HTTP/2 gRPC");

        Console.WriteLine(
            "Remote binding        : IPv4 loopback, ephemeral port");

        Console.WriteLine(
            "Client authentication : Mutual TLS");

        Console.WriteLine(
            "Expected principal    : client-01");

        Console.WriteLine(
            "Property              : Environment temperature");

        Console.WriteLine(
            "Target identity       : Endpoint + generation + instrument "
            + "+ Property");

        Console.WriteLine(
            "Lifecycle ownership   : Runtime host only");

        Console.WriteLine();
    }

    private static void WriteResult(
        RuntimeHostNorthboundSnapshotComposition composition,
        PublishedRuntimeEndpointSnapshot endpointSnapshot,
        RuntimeHostPropertyTarget target,
        Uri secureAddress,
        GrpcV1.PropertyOperationResult response)
    {
        Console.WriteLine(
            $"Runtime host          : "
            + $"{composition.IdentityResolution.RuntimeHostId.Value}");

        Console.WriteLine(
            $"API version           : "
            + $"{Hase.Runtime.Northbound.RuntimeHostApiVersion.Current}");

        Console.WriteLine(
            $"Published endpoint     : {endpointSnapshot.EndpointId.Value}");

        Console.WriteLine(
            $"Attachment generation : {endpointSnapshot.Generation}");

        Console.WriteLine(
            $"Connection state      : "
            + $"{endpointSnapshot.ConnectionStatus.State}");

        Console.WriteLine(
            $"Secure gRPC address   : {secureAddress}");

        Console.WriteLine(
            "Authenticated principal: client-01");

        Console.WriteLine(
            $"Property              : {target.PropertyId.Value}");

        Console.WriteLine(
            $"Authoritative value   : "
            + $"{response.ConfirmedValue.Value.NumericValue}, "
            + $"{response.ConfirmedValue.TimestampUtc.ToDateTimeOffset():O}, "
            + $"{response.ConfirmedValue.Quality}");

        Console.WriteLine();

        Console.WriteLine(
            "Authenticated physical northbound Property validation "
            + "succeeded.");
    }
}
