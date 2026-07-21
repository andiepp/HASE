using System.Globalization;
using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Supervises a physical Arduino Uno-class compact serial endpoint and validates
/// automatic recovery after USB serial disconnection and reconnection.
/// </summary>
internal sealed class CapabilityC021Scenario
    : IParameterizedScenario
{
    private const int DefaultBaudRate =
        115200;

    private static readonly EndpointId PhysicalEndpointId =
        new(
            "arduino-uno-01");

    public string Name =>
        "c021";

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
        if (arguments.Count is < 1 or > 2)
        {
            throw new ArgumentException(
                "Capability C-021 requires a COM port and accepts an optional "
                + "baud rate.",
                nameof(arguments));
        }

        string portName =
            arguments[0];

        int baudRate =
            arguments.Count == 2
                ? ParseBaudRate(
                    arguments[1])
                : DefaultBaudRate;

        var transportOptions =
            new SerialTransportOptions(
                portName,
                baudRate);

        EndpointDescriptorDefinition descriptorDefinition =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreateDefinition();

        CompactPropertyMap propertyMap =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreatePropertyMap(
                    descriptorDefinition);

        var descriptorRepository =
            new InMemoryEndpointDescriptorRepository(
                [
                    new KeyValuePair<
                        DescriptorReference,
                        EndpointDescriptorDefinition>(
                            PhysicalArduinoUnoCompactDescriptorFactory
                                .DescriptorReference,
                            descriptorDefinition)
                ]);

        var connectionFactory =
            new CompactSerialEndpointConnector(
                new SystemIoPortsSerialByteStreamFactory(),
                descriptorRepository);

        var runtimeContext =
            new RuntimeContext();

        RuntimeEndpoint runtimeEndpoint =
            runtimeContext.AddEndpoint(
                descriptorDefinition.Materialize(
                    PhysicalEndpointId));

        var coordinator =
            new CompactRuntimeEndpointConnectionCoordinator(
                connectionFactory,
                transportOptions,
                propertyMap,
                runtimeEndpoint,
                new EndpointDescriptorCompatibilityValidator());

        var supervisor =
            new CompactRuntimeEndpointConnectionSupervisor(
                coordinator,
                propertyMap,
                new DefaultRuntimeEndpointReconnectPolicy(),
                CompactEndpointHealthProbeOptions.Default);

        var statusObserver =
            new ConsoleConnectionStatusObserver(
                runtimeEndpoint);

        runtimeEndpoint.SubscribeConnectionStatus(
            statusObserver);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        ConsoleCancelEventHandler cancelHandler =
            (_, eventArgs) =>
            {
                eventArgs.Cancel =
                    true;

                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    Console.WriteLine();

                    Console.WriteLine(
                        "Stopping compact endpoint supervision.");

                    cancellationTokenSource.Cancel();
                }
            };

        Console.CancelKeyPress +=
            cancelHandler;

        WriteHeader(
            transportOptions);

        try
        {
            await supervisor.RunAsync(
                cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
            when (cancellationTokenSource.IsCancellationRequested)
        {
            Console.WriteLine();

            Console.WriteLine(
                "Compact endpoint supervision stopped.");

            Console.WriteLine();

            Console.WriteLine(
                $"Final connection state : "
                + $"{runtimeEndpoint.ConnectionStatus.State}");

            Console.WriteLine(
                $"Published endpoints    : "
                + $"{runtimeContext.Endpoints.Count}");

            WriteCachedProperty(
                runtimeEndpoint,
                prefix:
                    "Final cached value");
        }
        finally
        {
            Console.CancelKeyPress -=
                cancelHandler;

            runtimeEndpoint.UnsubscribeConnectionStatus(
                statusObserver);
        }
    }

    private static int ParseBaudRate(
        string value)
    {
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int baudRate)
            || baudRate <= 0)
        {
            throw new ArgumentException(
                $"'{value}' is not a valid positive baud rate.",
                nameof(value));
        }

        return baudRate;
    }

    private static void WriteHeader(
        SerialTransportOptions transportOptions)
    {
        const string title =
            "Capability C-021";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Supervise the physical Arduino Uno compact serial endpoint and "
            + "recover automatically after USB disconnection.");

        Console.WriteLine();

        Console.WriteLine(
            $"Port                 : {transportOptions.PortName}");

        Console.WriteLine(
            $"Baud rate            : {transportOptions.BaudRate}");

        Console.WriteLine(
            "Connection origin    : Configured");

        Console.WriteLine(
            "Protocol             : Compact Serial Protocol V1");

        Console.WriteLine(
            "Descriptor source    : Host repository");

        Console.WriteLine(
            $"Expected endpoint    : {PhysicalEndpointId.Value}");

        Console.WriteLine(
            $"Descriptor reference : "
            + $"{PhysicalArduinoUnoCompactDescriptorFactory.DescriptorReference.Id.Value} "
            + $"v{PhysicalArduinoUnoCompactDescriptorFactory.DescriptorReference.Version}");

        Console.WriteLine(
            $"Probe interval       : "
            + $"{CompactEndpointHealthProbeOptions.Default.ProbeInterval}");

        Console.WriteLine(
            $"Probe timeout        : "
            + $"{CompactEndpointHealthProbeOptions.Default.ProbeTimeout}");

        Console.WriteLine(
            "Reconnect schedule   : immediate, 1 s, 2 s, 5 s, 10 s maximum");

        Console.WriteLine();

        Console.WriteLine(
            "After the endpoint reaches Ready:");

        Console.WriteLine();

        Console.WriteLine(
            "1. Unplug the Arduino Uno USB connection.");

        Console.WriteLine(
            "2. Wait for the endpoint to enter Faulted and begin retrying.");

        Console.WriteLine(
            "3. Reconnect the Arduino on the same COM port.");

        Console.WriteLine(
            "4. Wait for Synchronizing and Ready.");

        Console.WriteLine(
            "5. Press Ctrl+C to stop.");

        Console.WriteLine();

        Console.WriteLine(
            "Starting compact endpoint supervision.");

        Console.WriteLine();
    }

    private static void WriteCachedProperty(
        RuntimeEndpoint runtimeEndpoint,
        string prefix)
    {
        RuntimeProperty? runtimeProperty =
            runtimeEndpoint
                .FindInstrument(
                    PhysicalArduinoUnoCompactDescriptorFactory
                        .ControllerInstrumentId)
                ?.FindProperty(
                    PhysicalArduinoUnoCompactDescriptorFactory
                        .BuiltInLedStatePropertyId);

        if (runtimeProperty?.CurrentValue is not PropertyValue propertyValue)
        {
            Console.WriteLine(
                $"{prefix,-23}: (empty)");

            return;
        }

        string value =
            propertyValue.Value is bool booleanValue
                ? booleanValue
                    ? "On"
                    : "Off"
                : propertyValue.Value?.ToString()
                    ?? "(null)";

        Console.WriteLine(
            $"{prefix,-23}: {value}");

        Console.WriteLine(
            $"Timestamp UTC           : "
            + $"{propertyValue.TimestampUtc:O}");

        Console.WriteLine(
            $"Quality                 : "
            + $"{propertyValue.Quality}");
    }

    private sealed class ConsoleConnectionStatusObserver
        : IEndpointConnectionStatusObserver
    {
        private readonly RuntimeEndpoint _runtimeEndpoint;

        public ConsoleConnectionStatusObserver(
            RuntimeEndpoint runtimeEndpoint)
        {
            _runtimeEndpoint =
                runtimeEndpoint;
        }

        public void OnEndpointConnectionStatusChanged(
            EndpointConnectionStatusChanged change)
        {
            Console.WriteLine(
                $"Connection state       : "
                + $"{change.PreviousStatus.State} -> "
                + $"{change.CurrentStatus.State}");

            if (!string.IsNullOrWhiteSpace(
                    change.CurrentStatus.Detail))
            {
                Console.WriteLine(
                    $"Status detail         : "
                    + $"{change.CurrentStatus.Detail}");
            }

            if (change.CurrentStatus.State
                == EndpointConnectionState.Ready)
            {
                WriteCachedProperty(
                    _runtimeEndpoint,
                    prefix:
                        "Synchronized cache");
            }

            Console.WriteLine();
        }
    }
}