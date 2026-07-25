using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates normalized northbound live observation against physical native
/// and compact endpoints while the runtime host retains lifecycle ownership.
/// </summary>
internal sealed class CapabilityC028Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    private const ushort ArduinoVendorId =
        0x2341;

    private const ushort ArduinoUnoProductId =
        0x0043;

    private static readonly RuntimeHostId RuntimeHostId =
        new(
            "protocol-explorer-physical-validation");

    private static readonly TimeSpan ObservationTimeout =
        TimeSpan.FromMinutes(
            2);

    public string Name =>
        "c028";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        CapabilityC028Arguments parsedArguments =
            CapabilityC028Arguments.Parse(
                arguments);

        ExecuteAsync(
                parsedArguments)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task ExecuteAsync(
        CapabilityC028Arguments arguments)
    {
        switch (arguments.EndpointFamily)
        {
            case CapabilityC028EndpointFamily.Esp32:
                await ExecuteEsp32Async(
                    arguments.Esp32Host!);
                break;

            case CapabilityC028EndpointFamily.Arduino:
                await ExecuteArduinoAsync(
                    arguments.BaudRate,
                    arguments.VerificationTimeout);
                break;

            default:
                throw new InvalidOperationException(
                    "The Capability C-028 endpoint family is not supported.");
        }
    }

    private static async Task ExecuteEsp32Async(
        string endpointHost)
    {
        WriteHeader(
            "Native Protocol Version 1",
            $"Host {endpointHost}, port {TcpPort}",
            "GPIO17 pushbutton");

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
                        endpointHost,
                        TcpPort),
                    PhysicalEnvironmentEndpointDescriptorFactory.EndpointId),
                EndpointProvidedDescriptorSource.Instance);

        await ExecuteObservationAsync(
            attachmentHost,
            request,
            PhysicalEnvironmentEndpointDescriptorFactory.InstrumentId,
            PhysicalEnvironmentEndpointDescriptorFactory
                .TemperaturePropertyId,
            PhysicalEnvironmentEndpointDescriptorFactory
                .ControllerInstrumentId,
            PhysicalEnvironmentEndpointDescriptorFactory
                .ButtonPressedEventPath,
            "Press and release the ESP32 GPIO17 pushbutton once.");
    }

    private static async Task ExecuteArduinoAsync(
        int baudRate,
        TimeSpan verificationTimeout)
    {
        CompactEndpointDefinition compactDefinition =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreateCompactDefinition();

        var definitionRepository =
            new InMemoryCompactEndpointDefinitionRepository(
                [
                    compactDefinition
                ]);

        var descriptorRepository =
            new CompactEndpointDescriptorRepositoryAdapter(
                definitionRepository);

        UsbSerialEndpointDiscoveryService discoveryService =
            WindowsUsbSerialEndpointDiscovery.Create(
                descriptorRepository,
                new UsbSerialEndpointMetadataFilter(
                    vendorId:
                        ArduinoVendorId,
                    productId:
                        ArduinoUnoProductId));

        var discoveryOptions =
            new UsbSerialEndpointDiscoveryOptions(
                baudRate,
                verificationTimeout);

        WriteHeader(
            "Compact Serial Protocol V1",
            $"VID 0x{ArduinoVendorId:X4}, PID 0x{ArduinoUnoProductId:X4}, "
            + $"{baudRate} baud",
            "Arduino Uno D7 pushbutton");

        Console.WriteLine(
            "Discovering and authoritatively verifying the Arduino Uno.");

        Console.WriteLine();

        UsbSerialEndpointDiscoveryResult discoveryResult =
            await discoveryService.DiscoverAsync(
                discoveryOptions);

        if (discoveryResult.VerifiedEndpoints.Count != 1)
        {
            throw new InvalidOperationException(
                "Capability C-028 requires exactly one authoritatively "
                + "verified Arduino Uno endpoint after VID/PID filtering, "
                + $"but found {discoveryResult.VerifiedEndpoints.Count}.");
        }

        VerifiedUsbSerialEndpoint selectedEndpoint =
            discoveryResult.VerifiedEndpoints[0];

        Console.WriteLine(
            $"Verified port         : {selectedEndpoint.Candidate.PortName}");

        Console.WriteLine(
            $"Authoritative endpoint: {selectedEndpoint.EndpointId.Value}");

        Console.WriteLine();

        await using RuntimeEndpointAttachmentHost attachmentHost =
            RuntimeEndpointAttachmentHost.CreateCompactSerial(
                definitionRepository,
                new DefaultRuntimeEndpointReconnectPolicy(),
                CompactEndpointHealthProbeOptions.Default);

        var request =
            new EndpointAttachmentRequest(
                SerialEndpointConnectionDefinition.FromVerifiedEndpoint(
                    selectedEndpoint,
                    discoveryOptions),
                HostRepositoryDescriptorSource.Instance);

        await ExecuteObservationAsync(
            attachmentHost,
            request,
            PhysicalArduinoUnoCompactDescriptorFactory
                .ControllerInstrumentId,
            PhysicalArduinoUnoCompactDescriptorFactory
                .BuiltInLedStatePropertyId,
            PhysicalArduinoUnoCompactDescriptorFactory
                .ControllerInstrumentId,
            PhysicalArduinoUnoCompactDescriptorFactory
                .ButtonPressedEventPath,
            "Press and release the Arduino Uno D7 pushbutton once.");
    }

    private static async Task ExecuteObservationAsync(
        RuntimeEndpointAttachmentHost attachmentHost,
        EndpointAttachmentRequest request,
        InstrumentId propertyInstrumentId,
        PropertyId propertyId,
        InstrumentId eventInstrumentId,
        DescriptorPath eventPath,
        string userAction)
    {
        await using RuntimeHostNorthboundSnapshotComposition composition =
            await RuntimeHostNorthboundSnapshotComposition
                .CreateFileBackedAsync(
                    attachmentHost.AttachmentInventory,
                    Path.Combine(
                        Path.GetTempPath(),
                        "hase-protocol-explorer",
                        "runtime-host-identity.json"),
                    RuntimeHostId);

        await using RuntimeHostObservationSubscription subscription =
            await composition.ObservationService.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions());

        if (subscription.InitialSnapshot.Endpoints.Count != 0)
        {
            throw new InvalidDataException(
                "The C-028 subscription must open before physical "
                + "attachment publication.");
        }

        using var cancellationSource =
            new CancellationTokenSource(
                ObservationTimeout);

        RuntimeEndpointAttachmentInventoryEntry? entry =
            null;

        bool detached =
            false;

        try
        {
            Console.WriteLine(
                "Northbound observation subscription opened.");

            Console.WriteLine(
                "Initial published endpoints: 0");

            Console.WriteLine();

            entry =
                await attachmentHost.AttachmentInventory.AttachAsync(
                    request,
                    cancellationSource.Token);

            RuntimeHostObservation publication =
                await ReadObservationAsync(
                    subscription,
                    entry.EndpointId,
                    RuntimeHostObservationKind.AttachmentPublished,
                    cancellationSource.Token);

            var publishedPayload =
                (RuntimeHostAttachmentPublishedObservationPayload)
                    publication.Payload;

            var propertyTarget =
                new RuntimeHostPropertyTarget(
                    publication.EndpointId,
                    publication.AttachmentGeneration,
                    propertyInstrumentId,
                    propertyId);

            RuntimeHostPropertyOperationResult propertyRead =
                await composition.PropertyService.ReadAsync(
                    propertyTarget,
                    cancellationSource.Token);

            if (!propertyRead.IsSuccess)
            {
                throw new InvalidDataException(
                    $"Authoritative Property read failed with status "
                    + $"'{propertyRead.Status}'."
                    + FormatDiagnostic(
                        propertyRead.Diagnostic));
            }

            RuntimeHostObservation propertyObservation =
                await ReadObservationAsync(
                    subscription,
                    entry.EndpointId,
                    RuntimeHostObservationKind.PropertyValueChanged,
                    cancellationSource.Token);

            var propertyPayload =
                (RuntimeHostPropertyValueChangedObservationPayload)
                    propertyObservation.Payload;

            if (propertyPayload.InstrumentId
                    != propertyInstrumentId
                || propertyPayload.PropertyId
                    != propertyId)
            {
                throw new InvalidDataException(
                    "The observed Property identity does not match the "
                    + "authoritative read target.");
            }

            Console.WriteLine(
                userAction);

            Console.WriteLine();

            RuntimeHostObservation eventObservation =
                await ReadObservationAsync(
                    subscription,
                    entry.EndpointId,
                    RuntimeHostObservationKind.EventOccurred,
                    cancellationSource.Token);

            var eventPayload =
                (RuntimeHostEventOccurredObservationPayload)
                    eventObservation.Payload;

            if (eventPayload.InstrumentId
                    != eventInstrumentId
                || eventPayload.EventPath
                    != eventPath)
            {
                throw new InvalidDataException(
                    "The observed Event identity does not match the physical "
                    + "button Event.");
            }

            if (eventPayload.Value is not null)
            {
                throw new InvalidDataException(
                    "The physical button Event unexpectedly carried a value.");
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

            await ReadObservationAsync(
                subscription,
                entry.EndpointId,
                RuntimeHostObservationKind.AttachmentEnded,
                cancellationSource.Token);

            Console.WriteLine(
                "Capability C-028 physical live-observation validation "
                + "succeeded.");

            Console.WriteLine();

            Console.WriteLine(
                $"Runtime host          : "
                + $"{composition.IdentityResolution.RuntimeHostId.Value}");

            Console.WriteLine(
                $"API version           : {RuntimeHostApiVersion.Current}");

            Console.WriteLine(
                $"Published endpoint     : "
                + $"{publishedPayload.Endpoint.EndpointId.Value}");

            Console.WriteLine(
                $"Attachment generation : "
                + $"{publication.AttachmentGeneration}");

            Console.WriteLine(
                "Observed milestones    : Publication -> Property -> Event "
                + "-> Ending");

            Console.WriteLine(
                "Intermediate updates  : Permitted and retained");

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

    private static async Task<RuntimeHostObservation> ReadObservationAsync(
        RuntimeHostObservationSubscription subscription,
        EndpointId endpointId,
        RuntimeHostObservationKind expectedKind,
        CancellationToken cancellationToken)
    {
        await foreach (
            RuntimeHostObservation observation
            in subscription.ReadAllAsync(
                cancellationToken))
        {
            Console.WriteLine(
                RuntimeHostObservationFormatter.Format(
                    observation));

            Console.WriteLine();

            if (observation.EndpointId == endpointId
                && observation.Kind == expectedKind)
            {
                return observation;
            }
        }

        throw new InvalidDataException(
            $"The observation subscription ended before '{expectedKind}' "
            + "was observed.");
    }

    private static string FormatDiagnostic(
        string? diagnostic)
    {
        return diagnostic is null
            ? string.Empty
            : $" Diagnostic: {diagnostic}";
    }

    private static void WriteHeader(
        string endpointFamily,
        string connection,
        string physicalEvent)
    {
        const string title =
            "Capability C-028";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Validate normalized northbound live observation against a "
            + "physical endpoint.");

        Console.WriteLine();

        Console.WriteLine(
            $"Endpoint family       : {endpointFamily}");

        Console.WriteLine(
            $"Connection            : {connection}");

        Console.WriteLine(
            $"Physical Event        : {physicalEvent}");

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
            "Lifecycle ownership    : Runtime host only");

        Console.WriteLine();
    }
}