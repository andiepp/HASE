using Grpc.Core;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Northbound;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Tcp;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates authenticated northbound observation against the physical ESP32
/// while the runtime host retains endpoint lifecycle ownership.
/// </summary>
internal sealed class CapabilityC034Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    private static readonly RuntimeHostId RuntimeHostId =
        new(
            "protocol-explorer-authenticated-observation-validation");

    private static readonly TimeSpan ObservationTimeout =
        TimeSpan.FromMinutes(
            2);

    public string Name =>
        "c034";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        CapabilityC034Arguments parsedArguments =
            ParseArguments(
                arguments);

        ExecuteAsync(
                parsedArguments)
            .GetAwaiter()
            .GetResult();
    }

    internal static CapabilityC034Arguments ParseArguments(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count != 1)
        {
            throw new ArgumentException(
                "Capability C-034 requires exactly one ESP32 host name "
                + "or IP address.",
                nameof(arguments));
        }

        return new CapabilityC034Arguments(
            arguments[0]);
    }

    private static async Task ExecuteAsync(
        CapabilityC034Arguments arguments)
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

        var request =
            new EndpointAttachmentRequest(
                NetworkEndpointConnectionDefinition.FromConfiguration(
                    new TcpTransportOptions(
                        arguments.EndpointHost,
                        TcpPort),
                    PhysicalEnvironmentEndpointDescriptorFactory.EndpointId),
                EndpointProvidedDescriptorSource.Instance);

        await using RuntimeHostNorthboundSnapshotComposition composition =
            await RuntimeHostNorthboundSnapshotComposition
                .CreateFileBackedAsync(
                    attachmentHost.AttachmentInventory,
                    Path.Combine(
                        Path.GetTempPath(),
                        "hase-protocol-explorer",
                        "runtime-host-identity.json"),
                    RuntimeHostId);

        DateTimeOffset validationTimeUtc =
            DateTimeOffset.UtcNow;

        await using CapabilityC034SecureHostComposition secureHost =
            await CapabilityC034SecureHostComposition.CreateAsync(
                composition.SnapshotProvider,
                composition.ObservationService,
                validationTimeUtc);

        Uri secureAddress =
            await secureHost.StartAsync();

        using CapabilityC034SecureGrpcClient client =
            CapabilityC034SecureGrpcClient.Create(
                secureAddress,
                secureHost.AuthenticationComposition
                    .Certificates
                    .ClientCertificate,
                secureHost.AuthenticationComposition
                    .Certificates
                    .ServerCertificate);
        using var cancellationSource =
            new CancellationTokenSource(
                ObservationTimeout);
        using AsyncServerStreamingCall<GrpcV1.ObserveResponse> call =
            client.Observe(
                DateTime.UtcNow.Add(
                    ObservationTimeout),
                cancellationSource.Token);

        GrpcV1.ObserveResponse initialResponse =
            await ReadNextAsync(
                call,
                cancellationSource.Token);

        if (initialResponse.ContentCase
                != GrpcV1.ObserveResponse.ContentOneofCase.InitialSnapshot
            || initialResponse.InitialSnapshot.Snapshot.Endpoints.Count != 0)
        {
            throw new InvalidDataException(
                "The authenticated C-034 subscription must open with an "
                + "empty snapshot before physical attachment.");
        }

        RuntimeEndpointAttachmentInventoryEntry? entry =
            null;
        bool detached =
            false;
        ulong lastSequence =
            initialResponse.InitialSnapshot.SnapshotSequence;

        try
        {
            Console.WriteLine(
                "Authenticated northbound observation subscription opened.");
            Console.WriteLine(
                "Initial published endpoints: 0");
            Console.WriteLine();

            entry =
                await attachmentHost.AttachmentInventory.AttachAsync(
                    request,
                    cancellationSource.Token);

            GrpcV1.RuntimeHostObservation publication =
                await ReadObservationAsync(
                    call,
                    entry.EndpointId,
                    GrpcV1.RuntimeHostObservationKind.AttachmentPublished,
                    lastSequence,
                    cancellationSource.Token);
            lastSequence =
                publication.Sequence;

            PublishedRuntimeEndpointSnapshot endpointSnapshot =
                composition.InventorySnapshotProvider
                    .List()
                    .Single(
                        candidate =>
                            candidate.EndpointId
                            == entry.EndpointId);
            var propertyTarget =
                new RuntimeHostPropertyTarget(
                    endpointSnapshot.EndpointId,
                    endpointSnapshot.Generation,
                    PhysicalEnvironmentEndpointDescriptorFactory.InstrumentId,
                    PhysicalEnvironmentEndpointDescriptorFactory
                        .TemperaturePropertyId);
            RuntimeHostPropertyOperationResult propertyRead =
                await composition.PropertyService.ReadAsync(
                    propertyTarget,
                    cancellationSource.Token);

            if (!propertyRead.IsSuccess)
            {
                throw new InvalidDataException(
                    $"Authoritative Property read failed with status "
                    + $"'{propertyRead.Status}'.");
            }

            GrpcV1.RuntimeHostObservation propertyObservation =
                await ReadObservationAsync(
                    call,
                    entry.EndpointId,
                    GrpcV1.RuntimeHostObservationKind.PropertyValueChanged,
                    lastSequence,
                    cancellationSource.Token);
            lastSequence =
                propertyObservation.Sequence;

            if (propertyObservation.PropertyValueChanged.InstrumentId
                    != PhysicalEnvironmentEndpointDescriptorFactory
                        .InstrumentId.Value
                || propertyObservation.PropertyValueChanged.PropertyId
                    != PhysicalEnvironmentEndpointDescriptorFactory
                        .TemperaturePropertyId.Value)
            {
                throw new InvalidDataException(
                    "The observed Property identity does not match the "
                    + "physical temperature target.");
            }

            Console.WriteLine(
                "Press and release the ESP32 GPIO17 pushbutton once.");
            Console.WriteLine();

            GrpcV1.RuntimeHostObservation eventObservation =
                await ReadObservationAsync(
                    call,
                    entry.EndpointId,
                    GrpcV1.RuntimeHostObservationKind.EventOccurred,
                    lastSequence,
                    cancellationSource.Token);
            lastSequence =
                eventObservation.Sequence;

            if (eventObservation.EventOccurred.InstrumentId
                    != PhysicalEnvironmentEndpointDescriptorFactory
                        .ControllerInstrumentId.Value
                || !eventObservation.EventOccurred.EventPathSegments
                    .SequenceEqual(
                        PhysicalEnvironmentEndpointDescriptorFactory
                            .ButtonPressedEventPath
                            .Segments)
                || eventObservation.EventOccurred.Value is not null)
            {
                throw new InvalidDataException(
                    "The observed Event does not match the physical GPIO17 "
                    + "button Event.");
            }

            detached =
                await attachmentHost.AttachmentInventory.DetachAsync(
                    entry.EndpointId,
                    cancellationSource.Token);

            if (!detached)
            {
                throw new InvalidDataException(
                    "The physical endpoint was not detached orderly.");
            }

            GrpcV1.RuntimeHostObservation ending =
                await ReadObservationAsync(
                    call,
                    entry.EndpointId,
                    GrpcV1.RuntimeHostObservationKind.AttachmentEnded,
                    lastSequence,
                    cancellationSource.Token);

            Console.WriteLine(
                "Authenticated physical northbound observation validation "
                + "succeeded.");
            Console.WriteLine();
            Console.WriteLine(
                $"Runtime host          : {RuntimeHostId.Value}");
            Console.WriteLine(
                $"API version           : {RuntimeHostApiVersion.Current}");
            Console.WriteLine(
                $"Published endpoint     : {entry.EndpointId.Value}");
            Console.WriteLine(
                $"Attachment generation : {endpointSnapshot.Generation}");
            Console.WriteLine(
                $"Secure gRPC address   : {secureAddress}");
            Console.WriteLine(
                "Authenticated principal: client-01");
            Console.WriteLine(
                $"Final sequence         : {ending.Sequence}");
            Console.WriteLine(
                "Observed milestones    : Publication -> Property -> Event "
                + "-> Ending");
            Console.WriteLine(
                "Lifecycle ownership    : Runtime host only");
        }
        finally
        {
            if (entry is not null
                && !detached)
            {
                await attachmentHost.AttachmentInventory.DetachAsync(
                    entry.EndpointId);
            }
        }
    }

    private static async Task<GrpcV1.RuntimeHostObservation>
        ReadObservationAsync(
            AsyncServerStreamingCall<GrpcV1.ObserveResponse> call,
            EndpointId endpointId,
            GrpcV1.RuntimeHostObservationKind expectedKind,
            ulong lastSequence,
            CancellationToken cancellationToken)
    {
        while (true)
        {
            GrpcV1.ObserveResponse response =
                await ReadNextAsync(
                    call,
                    cancellationToken);

            if (response.ContentCase
                != GrpcV1.ObserveResponse.ContentOneofCase.Observation)
            {
                throw new InvalidDataException(
                    "The observation stream published a second initial "
                    + "snapshot.");
            }

            GrpcV1.RuntimeHostObservation observation =
                response.Observation;

            if (observation.Sequence <= lastSequence)
            {
                throw new InvalidDataException(
                    "The observation sequence is not strictly increasing.");
            }

            lastSequence =
                observation.Sequence;

            Console.WriteLine(
                $"Sequence              : {observation.Sequence}");
            Console.WriteLine(
                $"Kind                  : {observation.Kind}");
            Console.WriteLine(
                $"Endpoint              : {observation.EndpointId}");
            Console.WriteLine();

            if (observation.EndpointId == endpointId.Value
                && observation.Kind == expectedKind)
            {
                return observation;
            }
        }
    }

    private static async Task<GrpcV1.ObserveResponse> ReadNextAsync(
        AsyncServerStreamingCall<GrpcV1.ObserveResponse> call,
        CancellationToken cancellationToken)
    {
        if (!await call.ResponseStream.MoveNext(
                cancellationToken))
        {
            throw new InvalidDataException(
                "The authenticated observation stream ended unexpectedly.");
        }

        return call.ResponseStream.Current;
    }

    private static void WriteHeader(
        string endpointHost)
    {
        const string title =
            "Capability C-034";

        Console.WriteLine(
            title);
        Console.WriteLine(
            new string(
                '=',
                title.Length));
        Console.WriteLine();
        Console.WriteLine(
            "Validate authenticated physical northbound observation through "
            + "the mutual-TLS runtime host.");
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
            "Physical Event        : GPIO17 Controller.ButtonPressed");
        Console.WriteLine(
            "Initial snapshot       : Empty before attachment");
        Console.WriteLine(
            "Observed kinds         : AttachmentPublished, "
            + "PropertyValueChanged, EventOccurred, AttachmentEnded");
        Console.WriteLine(
            "Sequence scope         : Subscription-local");
        Console.WriteLine(
            "Replay                 : None");
        Console.WriteLine(
            "Lifecycle ownership   : Runtime host only");
        Console.WriteLine();
    }
}
