using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Northbound;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates authenticated physical Arduino Command execution through mutual
/// TLS while the runtime host retains endpoint lifecycle ownership.
/// </summary>
internal sealed class CapabilityC033Scenario
    : IParameterizedScenario
{
    private const ushort ArduinoVendorId =
        0x2341;

    private const ushort ArduinoUnoProductId =
        0x0043;

    private static readonly RuntimeHostId RuntimeHostId =
        new(
            "protocol-explorer-authenticated-command-validation");

    public string Name =>
        "c033";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        CapabilityC033Arguments parsedArguments =
            CapabilityC033Arguments.Parse(
                arguments);

        ExecuteAsync(
                parsedArguments)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task ExecuteAsync(
        CapabilityC033Arguments arguments)
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
                arguments.BaudRate,
                arguments.VerificationTimeout);

        WriteHeader(
            discoveryOptions);

        UsbSerialEndpointDiscoveryResult discoveryResult =
            await discoveryService.DiscoverAsync(
                discoveryOptions);

        if (discoveryResult.VerifiedEndpoints.Count != 1)
        {
            throw new InvalidOperationException(
                "Capability C-033 requires exactly one authoritatively "
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
            DateTimeOffset validationTimeUtc =
                DateTimeOffset.UtcNow;

            await using CapabilityC033SecureHostComposition
                secureHostComposition =
                    await CapabilityC033SecureHostComposition.CreateAsync(
                        northboundComposition.SnapshotProvider,
                        northboundComposition.PropertyService,
                        northboundComposition.CommandService,
                        validationTimeUtc);

            Uri secureAddress =
                await secureHostComposition.StartAsync();

            using CapabilityC033SecureGrpcClient client =
                CapabilityC033SecureGrpcClient.Create(
                    secureAddress,
                    secureHostComposition.AuthenticationComposition
                        .Certificates
                        .ClientCertificate,
                    secureHostComposition.AuthenticationComposition
                        .Certificates
                        .ServerCertificate);

            bool originalState =
                await ReadBooleanAsync(
                    client,
                    propertyTarget,
                    "Arduino original LED-state read");

            await ExecuteToggleAsync(
                client,
                commandTarget,
                "Arduino LED toggle");

            bool toggledState =
                await ReadBooleanAsync(
                    client,
                    propertyTarget,
                    "Arduino toggled LED-state read");

            if (toggledState
                == originalState)
            {
                throw new InvalidDataException(
                    "The authoritative Property read did not confirm a "
                    + "toggled Arduino LED state.");
            }

            await ExecuteToggleAsync(
                client,
                commandTarget,
                "Arduino LED restoration");

            bool restoredState =
                await ReadBooleanAsync(
                    client,
                    propertyTarget,
                    "Arduino restored LED-state read");

            if (restoredState
                != originalState)
            {
                throw new InvalidDataException(
                    "The authoritative Property read did not confirm "
                    + "restoration of the original Arduino LED state.");
            }

            WriteResult(
                northboundComposition,
                endpointSnapshot,
                secureAddress,
                originalState,
                toggledState,
                restoredState);
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
        return composition.InventorySnapshotProvider
                .List()
                .SingleOrDefault(
                    candidate =>
                        candidate.EndpointId
                        == endpointId)
            ?? throw new InvalidDataException(
                $"The northbound inventory did not publish endpoint "
                + $"'{endpointId.Value}'.");
    }

    private static async Task ExecuteToggleAsync(
        CapabilityC033SecureGrpcClient client,
        RuntimeHostCommandTarget target,
        string operationName)
    {
        GrpcV1.CommandOperationResult result =
            await client.ExecuteCommandAsync(
                target,
                argument:
                    null,
                DateTime.UtcNow.AddSeconds(
                    10));

        if (result.Status
            != GrpcV1.CommandOperationStatus.Success)
        {
            throw new InvalidDataException(
                $"{operationName} failed with status '{result.Status}'."
                + FormatDiagnostic(
                    result.HasDiagnostic
                        ? result.Diagnostic
                        : null));
        }

        if (result.ReturnValue is not null)
        {
            throw new InvalidDataException(
                $"{operationName} returned a value although the compact "
                + "Command has no return payload.");
        }
    }

    private static async Task<bool> ReadBooleanAsync(
        CapabilityC033SecureGrpcClient client,
        RuntimeHostPropertyTarget target,
        string operationName)
    {
        GrpcV1.PropertyOperationResult result =
            await client.ReadAuthoritativePropertyAsync(
                target,
                DateTime.UtcNow.AddSeconds(
                    10));

        if (result.Status
            != GrpcV1.PropertyOperationStatus.Success)
        {
            throw new InvalidDataException(
                $"{operationName} failed with status '{result.Status}'."
                + FormatDiagnostic(
                    result.HasDiagnostic
                        ? result.Diagnostic
                        : null));
        }

        if (result.ConfirmedValue?.Value is null
            || result.ConfirmedValue.Value.KindCase
                != GrpcV1.RemoteValue.KindOneofCase.BooleanValue)
        {
            throw new InvalidDataException(
                $"{operationName} did not return a Boolean confirmed value.");
        }

        return result.ConfirmedValue.Value.BooleanValue;
    }

    private static string FormatDiagnostic(
        string? diagnostic)
    {
        return diagnostic is null
            ? string.Empty
            : $" Diagnostic: {diagnostic}";
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
        UsbSerialEndpointDiscoveryOptions options)
    {
        const string title =
            "Capability C-033";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Validate authenticated physical Arduino Command execution "
            + "through the mutual-TLS runtime host.");

        Console.WriteLine();

        Console.WriteLine(
            "Endpoint family       : Compact Serial Protocol V1");

        Console.WriteLine(
            "Candidate filter      : VID 0x2341, PID 0x0043");

        Console.WriteLine(
            $"Baud rate             : {options.BaudRate}");

        Console.WriteLine(
            $"Verification timeout  : {options.VerificationTimeout}");

        Console.WriteLine(
            "Remote transport      : HTTPS / HTTP/2 gRPC");

        Console.WriteLine(
            "Remote binding        : IPv4 loopback, ephemeral port");

        Console.WriteLine(
            "Client authentication : Mutual TLS");

        Console.WriteLine(
            "Expected principal    : client-01");

        Console.WriteLine(
            "Command               : Led.Toggle");

        Console.WriteLine(
            "Authoritative Property: Led.State");

        Console.WriteLine(
            "Lifecycle ownership   : Runtime host only");

        Console.WriteLine(
            "State restoration     : Required");

        Console.WriteLine();
    }

    private static void WriteResult(
        RuntimeHostNorthboundSnapshotComposition composition,
        PublishedRuntimeEndpointSnapshot endpointSnapshot,
        Uri secureAddress,
        bool originalState,
        bool toggledState,
        bool restoredState)
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

        Console.WriteLine(
            $"Secure gRPC address   : {secureAddress}");

        Console.WriteLine(
            "Authenticated principal: client-01");

        Console.WriteLine(
            $"Original Property read : {FormatState(originalState)}");

        Console.WriteLine(
            $"Toggled Property read  : {FormatState(toggledState)}");

        Console.WriteLine(
            $"Restored Property read : {FormatState(restoredState)}");

        Console.WriteLine();

        Console.WriteLine(
            "Authenticated physical Arduino Command validation succeeded.");
    }

    private static string FormatState(
        bool value)
    {
        return value
            ? "On"
            : "Off";
    }
}
