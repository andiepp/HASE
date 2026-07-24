using System.Globalization;
using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Northbound;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates the public northbound Property service against physical native
/// and compact endpoints while the runtime host retains lifecycle ownership.
/// </summary>
internal sealed class CapabilityC026Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    private const int DefaultBaudRate =
        115200;

    private const int DefaultVerificationTimeoutSeconds =
        3;

    private const ushort ArduinoVendorId =
        0x2341;

    private const ushort ArduinoUnoProductId =
        0x0043;

    private static readonly RuntimeHostId RuntimeHostId =
        new(
            "protocol-explorer-physical-validation");

    public string Name =>
        "c026";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        CapabilityC026Arguments parsedArguments =
            ParseArguments(
                arguments);

        ExecuteAsync(
                parsedArguments)
            .GetAwaiter()
            .GetResult();
    }

    internal static CapabilityC026Arguments ParseArguments(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count == 0)
        {
            throw new ArgumentException(
                "Capability C-026 requires an endpoint family: "
                + "'esp32' or 'arduino'.",
                nameof(arguments));
        }

        if (string.Equals(
                arguments[0],
                "esp32",
                StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count != 2
                || string.IsNullOrWhiteSpace(
                    arguments[1]))
            {
                throw new ArgumentException(
                    "Capability C-026 ESP32 validation requires exactly "
                    + "one host name or IP address.",
                    nameof(arguments));
            }

            return CapabilityC026Arguments.ForEsp32(
                arguments[1]);
        }

        if (string.Equals(
                arguments[0],
                "arduino",
                StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count > 3)
            {
                throw new ArgumentException(
                    "Capability C-026 Arduino validation accepts an optional "
                    + "baud rate and an optional verification timeout in "
                    + "seconds.",
                    nameof(arguments));
            }

            int baudRate =
                arguments.Count >= 2
                    ? ParsePositiveInteger(
                        arguments[1],
                        "baud rate")
                    : DefaultBaudRate;

            int verificationTimeoutSeconds =
                arguments.Count == 3
                    ? ParsePositiveInteger(
                        arguments[2],
                        "verification timeout")
                    : DefaultVerificationTimeoutSeconds;

            return CapabilityC026Arguments.ForArduino(
                baudRate,
                TimeSpan.FromSeconds(
                    verificationTimeoutSeconds));
        }

        throw new ArgumentException(
            $"Unknown Capability C-026 endpoint family '{arguments[0]}'. "
            + "Expected 'esp32' or 'arduino'.",
            nameof(arguments));
    }

    private static async Task ExecuteAsync(
        CapabilityC026Arguments arguments)
    {
        switch (arguments.EndpointFamily)
        {
            case CapabilityC026EndpointFamily.Esp32:
                await ExecuteEsp32Async(
                    arguments.Esp32Host!);
                break;

            case CapabilityC026EndpointFamily.Arduino:
                await ExecuteArduinoAsync(
                    arguments.BaudRate,
                    arguments.VerificationTimeout);
                break;

            default:
                throw new InvalidOperationException(
                    "The Capability C-026 endpoint family is not supported.");
        }
    }

    private static async Task ExecuteEsp32Async(
        string endpointHost)
    {
        WriteEsp32Header(
            endpointHost);

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
                    endpointHost,
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

            RuntimeHostNorthboundSnapshotComposition composition =
                await CreateCompositionAsync(
                    attachmentHost);

            PublishedRuntimeEndpointSnapshot endpointSnapshot =
                GetPublishedEndpoint(
                    composition,
                    entry.EndpointId);

            var target =
                new RuntimeHostPropertyTarget(
                    endpointSnapshot.EndpointId,
                    endpointSnapshot.Generation,
                    PhysicalEnvironmentEndpointDescriptorFactory.InstrumentId,
                    PhysicalEnvironmentEndpointDescriptorFactory
                        .TemperaturePropertyId);

            RuntimeHostCachedPropertyResult cachedResult =
                composition.PropertyService.GetCached(
                    target);

            RuntimeHostPropertyOperationResult readResult =
                await composition.PropertyService.ReadAsync(
                    target);

            PropertyValue cachedValue =
                GetCachedValue(
                    cachedResult,
                    "ESP32 temperature cache");

            PropertyValue confirmedValue =
                GetConfirmedValue(
                    readResult,
                    "ESP32 authoritative temperature read");

            WriteCommonIdentity(
                composition,
                endpointSnapshot);

            Console.WriteLine(
                $"Property              : "
                + $"{target.PropertyId.Value}");

            WritePropertyValue(
                "Cached value",
                cachedValue);

            WritePropertyValue(
                "Authoritative read",
                confirmedValue);

            Console.WriteLine();

            Console.WriteLine(
                "Physical ESP32 northbound Property validation succeeded.");
        }
        finally
        {
            await DetachAsync(
                attachmentHost,
                entry);
        }
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
                baudRate,
                verificationTimeout);

        WriteArduinoHeader(
            discoveryOptions);

        UsbSerialEndpointDiscoveryResult discoveryResult =
            await discoveryService.DiscoverAsync(
                discoveryOptions);

        if (discoveryResult.VerifiedEndpoints.Count != 1)
        {
            throw new InvalidOperationException(
                "Capability C-026 requires exactly one authoritatively "
                + "verified Arduino Uno endpoint after VID/PID filtering, "
                + $"but found "
                + $"{discoveryResult.VerifiedEndpoints.Count}.");
        }

        VerifiedUsbSerialEndpoint selectedEndpoint =
            discoveryResult.VerifiedEndpoints[0];

        SerialEndpointConnectionDefinition connectionDefinition =
            SerialEndpointConnectionDefinition.FromVerifiedEndpoint(
                selectedEndpoint,
                discoveryOptions);

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
                connectionDefinition,
                HostRepositoryDescriptorSource.Instance);

        RuntimeEndpointAttachmentInventoryEntry? entry =
            null;

        try
        {
            Console.WriteLine(
                "Attaching the explicitly selected compact endpoint.");

            Console.WriteLine();

            entry =
                await attachmentHost.AttachmentInventory.AttachAsync(
                    request);

            RuntimeHostNorthboundSnapshotComposition composition =
                await CreateCompositionAsync(
                    attachmentHost);

            PublishedRuntimeEndpointSnapshot endpointSnapshot =
                GetPublishedEndpoint(
                    composition,
                    entry.EndpointId);

            var target =
                new RuntimeHostPropertyTarget(
                    endpointSnapshot.EndpointId,
                    endpointSnapshot.Generation,
                    PhysicalArduinoUnoCompactDescriptorFactory
                        .ControllerInstrumentId,
                    PhysicalArduinoUnoCompactDescriptorFactory
                        .BuiltInLedStatePropertyId);

            RuntimeHostCachedPropertyResult cachedResult =
                composition.PropertyService.GetCached(
                    target);

            RuntimeHostPropertyOperationResult readResult =
                await composition.PropertyService.ReadAsync(
                    target);

            PropertyValue cachedValue =
                GetCachedValue(
                    cachedResult,
                    "Arduino LED cache");

            PropertyValue confirmedReadValue =
                GetConfirmedValue(
                    readResult,
                    "Arduino authoritative LED read");

            bool originalState =
                GetBooleanValue(
                    confirmedReadValue,
                    "Arduino authoritative LED read");

            RuntimeHostPropertyOperationResult writeResult =
                await composition.PropertyService.WriteAsync(
                    target,
                    !originalState);

            PropertyValue confirmedWriteValue =
                GetConfirmedValue(
                    writeResult,
                    "Arduino endpoint-confirmed LED write");

            bool writtenState =
                GetBooleanValue(
                    confirmedWriteValue,
                    "Arduino endpoint-confirmed LED write");

            if (writtenState == originalState)
            {
                throw new InvalidDataException(
                    "The Arduino endpoint did not confirm the requested "
                    + "toggled LED state.");
            }

            RuntimeHostPropertyOperationResult restoreResult =
                await composition.PropertyService.WriteAsync(
                    target,
                    originalState);

            PropertyValue restoredValue =
                GetConfirmedValue(
                    restoreResult,
                    "Arduino LED-state restoration");

            if (GetBooleanValue(
                    restoredValue,
                    "Arduino LED-state restoration")
                != originalState)
            {
                throw new InvalidDataException(
                    "The Arduino endpoint did not restore the original "
                    + "LED state.");
            }

            WriteCommonIdentity(
                composition,
                endpointSnapshot);

            Console.WriteLine(
                $"Property              : "
                + $"{target.PropertyId.Value}");

            WritePropertyValue(
                "Cached value",
                cachedValue);

            WritePropertyValue(
                "Authoritative read",
                confirmedReadValue);

            WritePropertyValue(
                "Confirmed write",
                confirmedWriteValue);

            WritePropertyValue(
                "Restored value",
                restoredValue);

            Console.WriteLine();

            Console.WriteLine(
                "Physical Arduino Uno northbound Property validation "
                + "succeeded.");
        }
        finally
        {
            await DetachAsync(
                attachmentHost,
                entry);
        }
    }

    private static Task<RuntimeHostNorthboundSnapshotComposition>
        CreateCompositionAsync(
            RuntimeEndpointAttachmentHost attachmentHost)
    {
        return RuntimeHostNorthboundSnapshotComposition.CreateFileBackedAsync(
            attachmentHost.AttachmentInventory,
            Path.Combine(
                Path.GetTempPath(),
                "hase-protocol-explorer",
                "runtime-host-identity.json"),
            RuntimeHostId);
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

    private static PropertyValue GetCachedValue(
        RuntimeHostCachedPropertyResult result,
        string operationName)
    {
        if (!result.IsSuccess)
        {
            throw CreateOperationException(
                operationName,
                result.Status,
                result.Diagnostic);
        }

        return result.Snapshot?.CurrentValue
            ?? throw new InvalidDataException(
                $"{operationName} did not contain a known cached value.");
    }

    private static PropertyValue GetConfirmedValue(
        RuntimeHostPropertyOperationResult result,
        string operationName)
    {
        if (!result.IsSuccess)
        {
            throw CreateOperationException(
                operationName,
                result.Status,
                result.Diagnostic);
        }

        return result.ConfirmedValue
            ?? throw new InvalidDataException(
                $"{operationName} did not contain an endpoint-confirmed "
                + "value.");
    }

    private static InvalidDataException CreateOperationException(
        string operationName,
        RuntimeHostPropertyOperationStatus status,
        string? diagnostic)
    {
        return new InvalidDataException(
            $"{operationName} failed with status '{status}'."
            + (
                diagnostic is null
                    ? string.Empty
                    : $" Diagnostic: {diagnostic}"));
    }

    private static bool GetBooleanValue(
        PropertyValue propertyValue,
        string operationName)
    {
        return propertyValue.Value
            is bool value
                ? value
                : throw new InvalidDataException(
                    $"{operationName} did not return a Boolean value.");
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

    private static int ParsePositiveInteger(
        string value,
        string fieldName)
    {
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int parsedValue)
            || parsedValue <= 0)
        {
            throw new ArgumentException(
                $"'{value}' is not a valid positive {fieldName}.",
                nameof(value));
        }

        return parsedValue;
    }

    private static void WriteCommonIdentity(
        RuntimeHostNorthboundSnapshotComposition composition,
        PublishedRuntimeEndpointSnapshot endpointSnapshot)
    {
        Console.WriteLine(
            $"Runtime host          : "
            + $"{composition.IdentityResolution.RuntimeHostId.Value}");

        Console.WriteLine(
            $"API version           : "
            + $"{RuntimeHostApiVersion.Current}");

        Console.WriteLine(
            $"Published endpoint     : {endpointSnapshot.EndpointId.Value}");

        Console.WriteLine(
            $"Attachment generation : {endpointSnapshot.Generation}");

        Console.WriteLine(
            $"Connection state      : "
            + $"{endpointSnapshot.ConnectionStatus.State}");
    }

    private static void WritePropertyValue(
        string label,
        PropertyValue value)
    {
        Console.WriteLine(
            $"{label,-22}: {value.Value}, "
            + $"{value.TimestampUtc:O}, "
            + $"{value.Quality}");
    }

    private static void WriteEsp32Header(
        string endpointHost)
    {
        WriteHeader();

        Console.WriteLine(
            "Endpoint family       : Native Protocol Version 1");

        Console.WriteLine(
            $"Host                  : {endpointHost}");

        Console.WriteLine(
            $"Port                  : {TcpPort}");

        Console.WriteLine(
            "Property              : Environment temperature");

        WriteBoundary();
    }

    private static void WriteArduinoHeader(
        UsbSerialEndpointDiscoveryOptions options)
    {
        WriteHeader();

        Console.WriteLine(
            "Endpoint family       : Compact Serial Protocol V1");

        Console.WriteLine(
            "Candidate filter      : VID 0x2341, PID 0x0043");

        Console.WriteLine(
            $"Baud rate             : {options.BaudRate}");

        Console.WriteLine(
            $"Verification timeout  : {options.VerificationTimeout}");

        Console.WriteLine(
            "Property              : Built-in LED state");

        WriteBoundary();
    }

    private static void WriteHeader()
    {
        const string title =
            "Capability C-026";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Validate physical Property access through the public "
            + "northbound runtime-host service.");

        Console.WriteLine();
    }

    private static void WriteBoundary()
    {
        Console.WriteLine(
            "Target identity       : Endpoint + generation + instrument "
            + "+ Property");

        Console.WriteLine(
            "Lifecycle ownership   : Runtime host only");

        Console.WriteLine(
            "Northbound exposure   : Snapshot and Property operations only");

        Console.WriteLine();
    }
}

internal enum CapabilityC026EndpointFamily
{
    Esp32,
    Arduino
}

internal sealed record CapabilityC026Arguments
{
    private CapabilityC026Arguments(
        CapabilityC026EndpointFamily endpointFamily,
        string? esp32Host,
        int baudRate,
        TimeSpan verificationTimeout)
    {
        EndpointFamily =
            endpointFamily;

        Esp32Host =
            esp32Host;

        BaudRate =
            baudRate;

        VerificationTimeout =
            verificationTimeout;
    }

    public CapabilityC026EndpointFamily EndpointFamily
    {
        get;
    }

    public string? Esp32Host
    {
        get;
    }

    public int BaudRate
    {
        get;
    }

    public TimeSpan VerificationTimeout
    {
        get;
    }

    public static CapabilityC026Arguments ForEsp32(
        string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            host);

        return new CapabilityC026Arguments(
            CapabilityC026EndpointFamily.Esp32,
            host,
            baudRate: 0,
            verificationTimeout:
                TimeSpan.Zero);
    }

    public static CapabilityC026Arguments ForArduino(
        int baudRate,
        TimeSpan verificationTimeout)
    {
        if (baudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baudRate));
        }

        if (verificationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(verificationTimeout));
        }

        return new CapabilityC026Arguments(
            CapabilityC026EndpointFamily.Arduino,
            esp32Host: null,
            baudRate,
            verificationTimeout);
    }
}
