using Hase.Core.Domain.Data;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates native endpoint attachment through the runtime-host lifecycle.
/// </summary>
internal sealed class CapabilityC016Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    public string Name =>
        "c016";

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
                "Capability C-016 requires exactly one argument: "
                + "the ESP32 host name or IP address.",
                nameof(arguments));
        }

        string host =
            arguments[0];

        WriteHeader(
            host);

        var runtimeContext =
            new RuntimeContext();

        IEndpointAttachmentService attachmentService =
            new NativeNetworkEndpointAttachmentService(
                runtimeContext,
                new ProtocolNativeEndpointBootstrapper(),
                new ProtocolRuntimeEndpointSynchronizer(
                    new EndpointDescriptorCompatibilityValidator()),
                new DefaultRuntimeEndpointReconnectPolicy(),
                MaximumPayloadLength);

        NetworkEndpointConnectionDefinition connectionDefinition =
            NetworkEndpointConnectionDefinition.FromConfiguration(
                new TcpTransportOptions(
                    host,
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

        IEndpointAttachmentSession? session =
            null;

        try
        {
            Console.WriteLine(
                "Attaching the endpoint through the runtime host.");

            Console.WriteLine();

            session =
                await attachmentService.AttachAsync(
                    request,
                    cancellationTokenSource.Token);

            WriteAttachedEndpoint(
                runtimeContext,
                session);

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
            when (cancellationTokenSource.IsCancellationRequested)
        {
            Console.WriteLine();

            Console.WriteLine(
                "Attachment shutdown requested.");
        }
        finally
        {
            Console.CancelKeyPress -=
                cancelHandler;

            if (session is not null)
            {
                await session.ShutdownAsync();

                WriteShutdownResult(
                    runtimeContext,
                    session);
            }
            else
            {
                Console.WriteLine();

                Console.WriteLine(
                    "No endpoint attachment session was published.");
            }
        }
    }

    private static void WriteAttachedEndpoint(
        RuntimeContext runtimeContext,
        IEndpointAttachmentSession session)
    {
        RuntimeEndpoint runtimeEndpoint =
            session.RuntimeEndpoint;

        Console.WriteLine(
            "Endpoint attachment completed.");

        Console.WriteLine();

        Console.WriteLine(
            $"Authoritative endpoint : "
            + $"{runtimeEndpoint.Descriptor.Id.Value}");

        Console.WriteLine(
            $"Connection state       : "
            + $"{runtimeEndpoint.ConnectionStatus.State}");

        Console.WriteLine(
            $"Published endpoints     : "
            + $"{runtimeContext.Endpoints.Count}");

        Console.WriteLine();

        Console.WriteLine(
            "Synchronized property cache:");

        WriteCachedProperties(
            runtimeEndpoint);

        Console.WriteLine();

        Console.WriteLine(
            "Press Ctrl+C to shut down the attachment.");
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

    private static void WriteShutdownResult(
        RuntimeContext runtimeContext,
        IEndpointAttachmentSession session)
    {
        Console.WriteLine();

        Console.WriteLine(
            "Endpoint attachment stopped.");

        Console.WriteLine();

        Console.WriteLine(
            $"Connection state   : "
            + $"{session.RuntimeEndpoint.ConnectionStatus.State}");

        Console.WriteLine(
            $"Published endpoints : "
            + $"{runtimeContext.Endpoints.Count}");
    }

    private static void WriteHeader(
        string host)
    {
        const string title =
            "Capability C-016";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Attach the physical ESP32/BME280 endpoint through the "
            + "runtime-host lifecycle.");

        Console.WriteLine();

        Console.WriteLine(
            $"Host                   : {host}");

        Console.WriteLine(
            $"Port                   : {TcpPort}");

        Console.WriteLine(
            "Connection origin      : Configured");

        Console.WriteLine(
            "Descriptor source      : Endpoint provided");

        Console.WriteLine(
            "Automatic publication  : Only after Ready");

        Console.WriteLine();
    }
}