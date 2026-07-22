using System.Globalization;
using Hase.CompactProtocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Discovers, explicitly selects, attaches, operates, and detaches one
/// physical Arduino Uno compact endpoint through the runtime-host inventory.
/// </summary>
internal sealed class CapabilityC024Scenario
    : IParameterizedScenario
{
    private const int DefaultBaudRate =
        115200;

    private const int DefaultVerificationTimeoutSeconds =
        3;

    private const ushort ArduinoVendorId =
        0x2341;

    private const ushort ArduinoUnoProductId =
        0x0043;

    public string Name =>
        "c024";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        (int baudRate, TimeSpan verificationTimeout) =
            ParseArguments(
                arguments);

        ExecuteAsync(
                baudRate,
                verificationTimeout)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task ExecuteAsync(
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

        WriteHeader(
            discoveryOptions);

        UsbSerialEndpointDiscoveryResult discoveryResult =
            await discoveryService.DiscoverAsync(
                discoveryOptions);

        if (discoveryResult.VerifiedEndpoints.Count != 1)
        {
            throw new InvalidOperationException(
                "Capability C-024 requires exactly one authoritatively "
                + "verified Arduino Uno endpoint after VID/PID filtering, "
                + $"but found "
                + $"{discoveryResult.VerifiedEndpoints.Count}.");
        }

        VerifiedUsbSerialEndpoint selectedEndpoint =
            discoveryResult.VerifiedEndpoints[0];

        WriteSelectedEndpoint(
            selectedEndpoint);

        SerialEndpointConnectionDefinition connectionDefinition =
            SerialEndpointConnectionDefinition.FromVerifiedEndpoint(
                selectedEndpoint,
                discoveryOptions);

        await using RuntimeEndpointAttachmentHost attachmentHost =
            RuntimeEndpointAttachmentHost.CreateCompactSerial(
                definitionRepository,
                new DefaultRuntimeEndpointReconnectPolicy(),
                CompactEndpointHealthProbeOptions.Default);

        var request =
            new EndpointAttachmentRequest(
                connectionDefinition,
                HostRepositoryDescriptorSource.Instance);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        ConsoleCancelEventHandler cancelHandler =
            (
                sender,
                eventArgs) =>
            {
                eventArgs.Cancel =
                    true;

                cancellationTokenSource.Cancel();
            };

        Console.CancelKeyPress +=
            cancelHandler;

        RuntimeEndpointAttachmentInventoryEntry? entry =
            null;

        try
        {
            Console.WriteLine(
                "Attaching the explicitly selected compact endpoint.");

            Console.WriteLine();

            entry =
                await attachmentHost.AttachmentInventory.AttachAsync(
                    request,
                    cancellationTokenSource.Token);

            WriteAttachedEndpoint(
                attachmentHost,
                entry,
                connectionDefinition);

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
            when (cancellationTokenSource.IsCancellationRequested)
        {
            Console.WriteLine();

            Console.WriteLine(
                "Compact endpoint detachment requested.");
        }
        finally
        {
            Console.CancelKeyPress -=
                cancelHandler;

            if (entry is not null)
            {
                bool detached =
                    await attachmentHost.AttachmentInventory.DetachAsync(
                        entry.EndpointId);

                WriteDetachmentResult(
                    attachmentHost,
                    entry,
                    detached);
            }
            else
            {
                Console.WriteLine();

                Console.WriteLine(
                    "No compact endpoint attachment inventory entry "
                    + "was added.");
            }
        }
    }

    internal static (int BaudRate, TimeSpan VerificationTimeout)
        ParseArguments(
            IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count > 2)
        {
            throw new ArgumentException(
                "Capability C-024 accepts an optional baud rate and an "
                + "optional verification timeout in seconds.",
                nameof(arguments));
        }

        int baudRate =
            arguments.Count >= 1
                ? ParsePositiveInteger(
                    arguments[0],
                    "baud rate")
                : DefaultBaudRate;

        int timeoutSeconds =
            arguments.Count == 2
                ? ParsePositiveInteger(
                    arguments[1],
                    "verification timeout")
                : DefaultVerificationTimeoutSeconds;

        return (
            baudRate,
            TimeSpan.FromSeconds(
                timeoutSeconds));
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

    private static void WriteHeader(
        UsbSerialEndpointDiscoveryOptions options)
    {
        const string title =
            "Capability C-024";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Discover and authoritatively verify the physical Arduino Uno, "
            + "explicitly select it, attach it through the runtime-host "
            + "inventory, synchronize Led.State, and detach it orderly.");

        Console.WriteLine();

        Console.WriteLine(
            "Platform             : Windows");

        Console.WriteLine(
            "Candidate filter     : VID 0x2341, PID 0x0043");

        Console.WriteLine(
            $"Baud rate            : {options.BaudRate}");

        Console.WriteLine(
            $"Verification timeout : {options.VerificationTimeout}");

        Console.WriteLine(
            "Protocol             : Compact Serial Protocol V1");

        Console.WriteLine(
            "Descriptor source    : Host repository");

        Console.WriteLine(
            $"Descriptor reference : "
            + $"{PhysicalArduinoUnoCompactDescriptorFactory.DescriptorReference.Id.Value} "
            + $"v{PhysicalArduinoUnoCompactDescriptorFactory.DescriptorReference.Version}");

        Console.WriteLine(
            "Attachment policy    : Explicit selection only");

        Console.WriteLine(
            "Inventory identity   : Authoritative EndpointId");

        Console.WriteLine(
            "Automatic replacement: Never");

        Console.WriteLine();

        Console.WriteLine(
            "USB metadata identifies candidates only.");

        Console.WriteLine(
            "CompactBootstrapResponse.EndpointId remains authoritative.");

        Console.WriteLine();

        Console.WriteLine(
            "Beginning discovery and authoritative verification.");

        Console.WriteLine();
    }

    private static void WriteSelectedEndpoint(
        VerifiedUsbSerialEndpoint selectedEndpoint)
    {
        Console.WriteLine(
            "Verified endpoint selected explicitly.");

        Console.WriteLine();

        Console.WriteLine(
            $"Candidate port        : "
            + $"{selectedEndpoint.Candidate.PortName}");

        Console.WriteLine(
            $"VID                   : "
            + $"{FormatIdentifier(selectedEndpoint.Candidate.VendorId)}");

        Console.WriteLine(
            $"PID                   : "
            + $"{FormatIdentifier(selectedEndpoint.Candidate.ProductId)}");

        Console.WriteLine(
            $"Product               : "
            + $"{selectedEndpoint.Candidate.ProductName ?? "Not reported"}");

        Console.WriteLine(
            $"Authoritative endpoint: "
            + $"{selectedEndpoint.EndpointId.Value}");

        Console.WriteLine(
            $"Descriptor reference  : "
            + $"{selectedEndpoint.DescriptorReference.Id.Value} "
            + $"v{selectedEndpoint.DescriptorReference.Version}");

        Console.WriteLine();
    }

    private static void WriteAttachedEndpoint(
        RuntimeEndpointAttachmentHost attachmentHost,
        RuntimeEndpointAttachmentInventoryEntry entry,
        SerialEndpointConnectionDefinition connectionDefinition)
    {
        RuntimeEndpoint runtimeEndpoint =
            entry.RuntimeEndpoint;

        RuntimeEndpointAttachmentInventoryEntry? foundEntry =
            attachmentHost.AttachmentInventory.Find(
                entry.EndpointId);

        RuntimeProperty runtimeProperty =
            runtimeEndpoint
                .FindInstrument(
                    PhysicalArduinoUnoCompactDescriptorFactory
                        .ControllerInstrumentId)!
                .FindProperty(
                    PhysicalArduinoUnoCompactDescriptorFactory
                        .BuiltInLedStatePropertyId)!;

        Console.WriteLine(
            "Compact endpoint inventory attachment completed.");

        Console.WriteLine();

        Console.WriteLine(
            $"Authoritative endpoint : {entry.EndpointId.Value}");

        Console.WriteLine(
            $"Connection origin      : {connectionDefinition.Origin}");

        Console.WriteLine(
            $"Operational port       : "
            + $"{connectionDefinition.TransportOptions.PortName}");

        Console.WriteLine(
            $"Connection state       : "
            + $"{runtimeEndpoint.ConnectionStatus.State}");

        Console.WriteLine(
            $"Inventory entries      : "
            + $"{attachmentHost.AttachmentInventory.List().Count}");

        Console.WriteLine(
            $"Authoritative lookup   : "
            + $"{(ReferenceEquals(entry, foundEntry) ? "Same entry" : "Failed")}");

        Console.WriteLine(
            $"Published endpoints    : "
            + $"{attachmentHost.RuntimeContext.Endpoints.Count}");

        Console.WriteLine(
            $"Led.State cache        : "
            + $"{FormatPropertyValue(runtimeProperty)}");

        Console.WriteLine();

        Console.WriteLine(
            "Discovery verification connection : Disposed");

        Console.WriteLine(
            "Attachment bootstrap connection    : Disposed");

        Console.WriteLine(
            "Operational connection             : Owned by attachment");

        Console.WriteLine();

        Console.WriteLine(
            "Press Ctrl+C to detach the compact endpoint.");
    }

    private static string FormatPropertyValue(
        RuntimeProperty runtimeProperty)
    {
        if (runtimeProperty.CurrentValue is null)
        {
            return "<no value>";
        }

        return
            $"{runtimeProperty.CurrentValue.Value}, "
            + $"{runtimeProperty.CurrentValue.TimestampUtc:O}, "
            + $"{runtimeProperty.CurrentValue.Quality}";
    }

    private static void WriteDetachmentResult(
        RuntimeEndpointAttachmentHost attachmentHost,
        RuntimeEndpointAttachmentInventoryEntry entry,
        bool detached)
    {
        Console.WriteLine();

        Console.WriteLine(
            "Compact endpoint inventory detachment completed.");

        Console.WriteLine();

        Console.WriteLine(
            $"Detached               : {detached}");

        Console.WriteLine(
            $"Connection state       : "
            + $"{entry.RuntimeEndpoint.ConnectionStatus.State}");

        Console.WriteLine(
            $"Inventory entries      : "
            + $"{attachmentHost.AttachmentInventory.List().Count}");

        Console.WriteLine(
            $"Published endpoints    : "
            + $"{attachmentHost.RuntimeContext.Endpoints.Count}");

        Console.WriteLine(
            "Operational connection : Disposed");
    }

    private static string FormatIdentifier(
        ushort? value)
    {
        return value.HasValue
            ? $"0x{value.Value:X4}"
            : "Not reported";
    }
}