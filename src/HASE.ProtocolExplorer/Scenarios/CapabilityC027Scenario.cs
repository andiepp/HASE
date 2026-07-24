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
/// Validates the public northbound Command service against physical native
/// and compact endpoints while the runtime host retains lifecycle ownership.
/// </summary>
internal sealed class CapabilityC027Scenario
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

    public string Name =>
        "c027";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        CapabilityC027Arguments parsedArguments =
            CapabilityC027Arguments.Parse(
                arguments);

        ExecuteAsync(
                parsedArguments)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task ExecuteAsync(
        CapabilityC027Arguments arguments)
    {
        switch (arguments.EndpointFamily)
        {
            case CapabilityC027EndpointFamily.Esp32:
                await ExecuteEsp32Async(
                    arguments.Esp32Host!);
                break;

            case CapabilityC027EndpointFamily.Arduino:
                await ExecuteArduinoAsync(
                    arguments.BaudRate,
                    arguments.VerificationTimeout);
                break;

            default:
                throw new InvalidOperationException(
                    "The Capability C-027 endpoint family is not supported.");
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

            var propertyTarget =
                new RuntimeHostPropertyTarget(
                    endpointSnapshot.EndpointId,
                    endpointSnapshot.Generation,
                    PhysicalEnvironmentEndpointDescriptorFactory
                        .ControllerInstrumentId,
                    PhysicalEnvironmentEndpointDescriptorFactory
                        .StatusLedEnabledPropertyId);

            var commandTarget =
                new RuntimeHostCommandTarget(
                    endpointSnapshot.EndpointId,
                    endpointSnapshot.Generation,
                    PhysicalEnvironmentEndpointDescriptorFactory
                        .ControllerInstrumentId,
                    PhysicalEnvironmentEndpointDescriptorFactory
                        .ToggleStatusLedCommandPath);

            bool originalState =
                await ReadBooleanAsync(
                    composition.PropertyService,
                    propertyTarget,
                    "ESP32 original LED-state read");

            RuntimeHostCommandOperationResult toggleResult =
                await composition.CommandService.ExecuteAsync(
                    commandTarget,
                    argument:
                        null);

            bool returnedToggleState =
                GetBooleanReturnValue(
                    toggleResult,
                    "ESP32 LED toggle");

            bool confirmedToggleState =
                await ReadBooleanAsync(
                    composition.PropertyService,
                    propertyTarget,
                    "ESP32 toggled LED-state read");

            ValidateToggledState(
                originalState,
                confirmedToggleState,
                "ESP32");

            if (returnedToggleState
                != confirmedToggleState)
            {
                throw new InvalidDataException(
                    "The ESP32 Command return value does not match the "
                    + "authoritative Property read.");
            }

            RuntimeHostCommandOperationResult restoreResult =
                await composition.CommandService.ExecuteAsync(
                    commandTarget,
                    argument:
                        null);

            bool returnedRestoreState =
                GetBooleanReturnValue(
                    restoreResult,
                    "ESP32 LED restoration");

            bool confirmedRestoreState =
                await ReadBooleanAsync(
                    composition.PropertyService,
                    propertyTarget,
                    "ESP32 restored LED-state read");

            ValidateRestoredState(
                originalState,
                confirmedRestoreState,
                "ESP32");

            if (returnedRestoreState
                != confirmedRestoreState)
            {
                throw new InvalidDataException(
                    "The ESP32 restoration return value does not match the "
                    + "authoritative Property read.");
            }

            WriteCommonIdentity(
                composition,
                endpointSnapshot);

            WriteValidationValues(
                originalState,
                returnedToggleState,
                confirmedToggleState,
                returnedRestoreState,
                confirmedRestoreState);

            Console.WriteLine();

            Console.WriteLine(
                "Physical ESP32 northbound Command validation succeeded.");
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
                "Capability C-027 requires exactly one authoritatively "
                + "verified Arduino Uno endpoint after VID/PID filtering, "
                + $"but found {discoveryResult.VerifiedEndpoints.Count}.");
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

            var propertyTarget =
                new RuntimeHostPropertyTarget(
                    endpointSnapshot.EndpointId,
                    endpointSnapshot.Generation,
                    PhysicalArduinoUnoCompactDescriptorFactory
                        .ControllerInstrumentId,
                    PhysicalArduinoUnoCompactDescriptorFactory
                        .BuiltInLedStatePropertyId);

            var commandTarget =
                new RuntimeHostCommandTarget(
                    endpointSnapshot.EndpointId,
                    endpointSnapshot.Generation,
                    PhysicalArduinoUnoCompactDescriptorFactory
                        .ControllerInstrumentId,
                    PhysicalArduinoUnoCompactDescriptorFactory
                        .ToggleBuiltInLedCommandPath);

            bool originalState =
                await ReadBooleanAsync(
                    composition.PropertyService,
                    propertyTarget,
                    "Arduino original LED-state read");

            RuntimeHostCommandOperationResult toggleResult =
                await composition.CommandService.ExecuteAsync(
                    commandTarget,
                    argument:
                        null);

            ValidateSuccessfulCompactCommand(
                toggleResult,
                "Arduino LED toggle");

            bool confirmedToggleState =
                await ReadBooleanAsync(
                    composition.PropertyService,
                    propertyTarget,
                    "Arduino toggled LED-state read");

            ValidateToggledState(
                originalState,
                confirmedToggleState,
                "Arduino");

            RuntimeHostCommandOperationResult restoreResult =
                await composition.CommandService.ExecuteAsync(
                    commandTarget,
                    argument:
                        null);

            ValidateSuccessfulCompactCommand(
                restoreResult,
                "Arduino LED restoration");

            bool confirmedRestoreState =
                await ReadBooleanAsync(
                    composition.PropertyService,
                    propertyTarget,
                    "Arduino restored LED-state read");

            ValidateRestoredState(
                originalState,
                confirmedRestoreState,
                "Arduino");

            WriteCommonIdentity(
                composition,
                endpointSnapshot);

            WriteValidationValues(
                originalState,
                returnedToggleState:
                    null,
                confirmedToggleState,
                returnedRestoreState:
                    null,
                confirmedRestoreState);

            Console.WriteLine();

            Console.WriteLine(
                "Physical Arduino Uno northbound Command validation "
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

    private static async Task<bool> ReadBooleanAsync(
        IRuntimeHostPropertyService propertyService,
        RuntimeHostPropertyTarget target,
        string operationName)
    {
        RuntimeHostPropertyOperationResult result =
            await propertyService.ReadAsync(
                target);

        if (!result.IsSuccess)
        {
            throw new InvalidDataException(
                $"{operationName} failed with status '{result.Status}'."
                + FormatDiagnostic(
                    result.Diagnostic));
        }

        PropertyValue confirmedValue =
            result.ConfirmedValue
            ?? throw new InvalidDataException(
                $"{operationName} did not contain an endpoint-confirmed "
                + "value.");

        return confirmedValue.Value
            is bool value
                ? value
                : throw new InvalidDataException(
                    $"{operationName} did not return a Boolean value.");
    }

    private static bool GetBooleanReturnValue(
        RuntimeHostCommandOperationResult result,
        string operationName)
    {
        ValidateSuccessfulCommand(
            result,
            operationName);

        return result.ReturnValue
            is bool value
                ? value
                : throw new InvalidDataException(
                    $"{operationName} did not return a Boolean value.");
    }

    private static void ValidateSuccessfulCompactCommand(
        RuntimeHostCommandOperationResult result,
        string operationName)
    {
        ValidateSuccessfulCommand(
            result,
            operationName);

        if (result.ReturnValue is not null)
        {
            throw new InvalidDataException(
                $"{operationName} returned a value although the compact "
                + "Command has no return payload.");
        }
    }

    private static void ValidateSuccessfulCommand(
        RuntimeHostCommandOperationResult result,
        string operationName)
    {
        if (!result.IsSuccess)
        {
            throw new InvalidDataException(
                $"{operationName} failed with status '{result.Status}'."
                + FormatDiagnostic(
                    result.Diagnostic));
        }
    }

    private static string FormatDiagnostic(
        string? diagnostic)
    {
        return diagnostic is null
            ? string.Empty
            : $" Diagnostic: {diagnostic}";
    }

    private static void ValidateToggledState(
        bool originalState,
        bool toggledState,
        string endpointName)
    {
        if (toggledState
            == originalState)
        {
            throw new InvalidDataException(
                $"The {endpointName} authoritative Property read did not "
                + "confirm a toggled LED state.");
        }
    }

    private static void ValidateRestoredState(
        bool originalState,
        bool restoredState,
        string endpointName)
    {
        if (restoredState
            != originalState)
        {
            throw new InvalidDataException(
                $"The {endpointName} authoritative Property read did not "
                + "confirm restoration of the original LED state.");
        }
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

    private static void WriteCommonIdentity(
        RuntimeHostNorthboundSnapshotComposition composition,
        PublishedRuntimeEndpointSnapshot endpointSnapshot)
    {
        Console.WriteLine(
            $"Runtime host          : "
            + $"{composition.IdentityResolution.RuntimeHostId.Value}");

        Console.WriteLine(
            $"API version           : {RuntimeHostApiVersion.Current}");

        Console.WriteLine(
            $"Published endpoint     : {endpointSnapshot.EndpointId.Value}");

        Console.WriteLine(
            $"Attachment generation : {endpointSnapshot.Generation}");

        Console.WriteLine(
            $"Connection state      : "
            + $"{endpointSnapshot.ConnectionStatus.State}");
    }

    private static void WriteValidationValues(
        bool originalState,
        bool? returnedToggleState,
        bool confirmedToggleState,
        bool? returnedRestoreState,
        bool confirmedRestoreState)
    {
        Console.WriteLine(
            $"Original Property read : {FormatState(originalState)}");

        Console.WriteLine(
            $"Toggle return value    : {FormatReturnValue(returnedToggleState)}");

        Console.WriteLine(
            $"Toggled Property read  : {FormatState(confirmedToggleState)}");

        Console.WriteLine(
            $"Restore return value   : {FormatReturnValue(returnedRestoreState)}");

        Console.WriteLine(
            $"Restored Property read : {FormatState(confirmedRestoreState)}");
    }

    private static string FormatReturnValue(
        bool? value)
    {
        return value.HasValue
            ? FormatState(
                value.Value)
            : "<none>";
    }

    private static string FormatState(
        bool value)
    {
        return value
            ? "On"
            : "Off";
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
            "Command               : Controller.ToggleStatusLed");

        Console.WriteLine(
            "Authoritative Property: Status LED enabled");

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
            "Command               : Led.Toggle");

        Console.WriteLine(
            "Authoritative Property: Built-in LED state");

        WriteBoundary();
    }

    private static void WriteHeader()
    {
        const string title =
            "Capability C-027";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Validate physical Command execution through the public "
            + "northbound runtime-host service.");

        Console.WriteLine();
    }

    private static void WriteBoundary()
    {
        Console.WriteLine(
            "Target identity       : Endpoint + generation + instrument "
            + "+ Command");

        Console.WriteLine(
            "Lifecycle ownership   : Runtime host only");

        Console.WriteLine(
            "Command argument      : null");

        Console.WriteLine(
            "Automatic retry       : Never");

        Console.WriteLine(
            "Cache update          : None from Command execution");

        Console.WriteLine(
            "State confirmation    : Authoritative Property reads");

        Console.WriteLine();
    }
}