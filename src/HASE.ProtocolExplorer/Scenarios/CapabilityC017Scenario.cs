using Hase.Core.Domain.Data;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates physical endpoint attachment through the runtime-host inventory.
/// </summary>
internal sealed class CapabilityC017Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    public string Name =>
        "c017";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        ExecuteAsync(
                arguments)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task ExecuteAsync(
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
        {
            throw new ArgumentException(
                "Capability C-017 requires exactly one argument: "
                + "the ESP32 host name or IP address.",
                nameof(arguments));
        }

        string endpointHost =
            arguments[0];

        WriteHeader(
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
                "Attaching the endpoint through the host inventory.");

            Console.WriteLine();

            entry =
                await attachmentHost.AttachmentInventory.AttachAsync(
                    request,
                    cancellationTokenSource.Token);

            WriteAttachedEndpoint(
                attachmentHost,
                entry);

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
            when (cancellationTokenSource.IsCancellationRequested)
        {
            Console.WriteLine();

            Console.WriteLine(
                "Inventory detachment requested.");
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
                    "No endpoint attachment inventory entry was added.");
            }
        }
    }

    private static void WriteAttachedEndpoint(
        RuntimeEndpointAttachmentHost attachmentHost,
        RuntimeEndpointAttachmentInventoryEntry entry)
    {
        RuntimeEndpoint runtimeEndpoint =
            entry.RuntimeEndpoint;

        RuntimeEndpointAttachmentInventoryEntry? foundEntry =
            attachmentHost.AttachmentInventory.Find(
                entry.EndpointId);

        Console.WriteLine(
            "Endpoint inventory attachment completed.");

        Console.WriteLine();

        Console.WriteLine(
            $"Authoritative endpoint : {entry.EndpointId.Value}");

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

        Console.WriteLine();

        Console.WriteLine(
            "Synchronized property cache:");

        WriteCachedProperties(
            runtimeEndpoint);

        Console.WriteLine();

        Console.WriteLine(
            "Press Ctrl+C to detach the inventory entry.");
    }

    private static void WriteCachedProperties(
        RuntimeEndpoint runtimeEndpoint)
    {
        foreach (
            RuntimeInstrument instrument
            in runtimeEndpoint.Instruments)
        {
            Console.WriteLine(
                $"  {instrument.Descriptor.Name}");

            foreach (
                RuntimeProperty property
                in instrument.Properties)
            {
                if (property.CurrentValue is null)
                {
                    Console.WriteLine(
                        $"    {property.Descriptor.DisplayName}: "
                        + "<no value>");

                    continue;
                }

                string unitSymbol =
                    property.Descriptor.Data
                        is NumericDataDescriptor numericData
                            ? numericData.NativeUnit.Symbol
                            : string.Empty;

                Console.WriteLine(
                    $"    {property.Descriptor.DisplayName}: "
                    + $"{property.CurrentValue.Value} "
                    + $"{unitSymbol}, "
                    + $"{property.CurrentValue.TimestampUtc:O}, "
                    + $"{property.CurrentValue.Quality}");
            }
        }
    }

    private static void WriteDetachmentResult(
        RuntimeEndpointAttachmentHost attachmentHost,
        RuntimeEndpointAttachmentInventoryEntry entry,
        bool detached)
    {
        Console.WriteLine();

        Console.WriteLine(
            "Endpoint inventory attachment stopped.");

        Console.WriteLine();

        Console.WriteLine(
            $"Detached             : {detached}");

        Console.WriteLine(
            $"Connection state      : "
            + $"{entry.RuntimeEndpoint.ConnectionStatus.State}");

        Console.WriteLine(
            $"Inventory entries     : "
            + $"{attachmentHost.AttachmentInventory.List().Count}");

        Console.WriteLine(
            $"Published endpoints   : "
            + $"{attachmentHost.RuntimeContext.Endpoints.Count}");
    }

    private static void WriteHeader(
        string endpointHost)
    {
        const string title =
            "Capability C-017";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Attach the physical ESP32/BME280 endpoint through the "
            + "runtime-host attachment inventory.");

        Console.WriteLine();

        Console.WriteLine(
            $"Host                   : {endpointHost}");

        Console.WriteLine(
            $"Port                   : {TcpPort}");

        Console.WriteLine(
            "Connection origin      : Configured");

        Console.WriteLine(
            "Descriptor source      : Endpoint provided");

        Console.WriteLine(
            "Inventory identity     : Authoritative EndpointId");

        Console.WriteLine(
            "Automatic replacement  : Never");

        Console.WriteLine();
    }
}