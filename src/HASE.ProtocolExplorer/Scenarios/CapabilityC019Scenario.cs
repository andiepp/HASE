using System.Globalization;
using Hase.CompactProtocol;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Instruments;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates physical compact command execution against an Arduino Uno-class
/// endpoint by toggling its built-in LED.
/// </summary>
internal sealed class CapabilityC019Scenario
    : IParameterizedScenario
{
    private const int DefaultBaudRate =
        115200;

    public string Name =>
        "c019";

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
                "Capability C-019 requires a COM port and accepts an optional "
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

        var descriptorRepository =
            new InMemoryEndpointDescriptorRepository(
                [
                    new KeyValuePair<
                        DescriptorReference,
                        EndpointDescriptorDefinition>(
                            PhysicalArduinoUnoCompactDescriptorFactory
                                .DescriptorReference,
                            PhysicalArduinoUnoCompactDescriptorFactory
                                .CreateDefinition())
                ]);

        var connector =
            new CompactSerialEndpointConnector(
                new SystemIoPortsSerialByteStreamFactory(),
                descriptorRepository);

        WriteHeader(
            transportOptions);

        Console.WriteLine(
            "Opening serial endpoint.");

        Console.WriteLine();

        await using CompactEndpointConnection connection =
            await connector.ConnectAsync(
                transportOptions,
                expectedEndpointId: null);

        CommandDescriptor command =
            FindToggleBuiltInLedCommand(
                connection.Descriptor);

        WriteConnectedEndpoint(
            connection,
            command);

        var executor =
            new CompactCommandExecutor(
                connection.Connection);

        Console.WriteLine(
            "Executing compact command.");

        Console.WriteLine();

        CompactCommandExecutionStatus status =
            await executor.ExecuteAsync(
                PhysicalArduinoUnoCompactDescriptorFactory
                    .ToggleBuiltInLedCompactCommandId);

        WriteCommandResult(
            status);
    }

    private static CommandDescriptor FindToggleBuiltInLedCommand(
        EndpointDescriptor descriptor)
    {
        InstrumentDescriptor? controller =
            descriptor.Instruments.FirstOrDefault(
                instrument =>
                    instrument.Id
                    == PhysicalArduinoUnoCompactDescriptorFactory
                        .ControllerInstrumentId);

        if (controller is null)
        {
            throw new InvalidDataException(
                "The materialized Arduino Uno descriptor does not contain "
                + "the expected GPIO controller instrument.");
        }

        CommandDescriptor? command =
            controller.Interface.FindCommand(
                PhysicalArduinoUnoCompactDescriptorFactory
                    .ToggleBuiltInLedCommandPath);

        if (command is null)
        {
            throw new InvalidDataException(
                "The materialized Arduino Uno descriptor does not contain "
                + "the expected built-in LED toggle command.");
        }

        return command;
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

    private static void WriteConnectedEndpoint(
        CompactEndpointConnection connection,
        CommandDescriptor command)
    {
        EndpointDescriptor descriptor =
            connection.Descriptor;

        Console.WriteLine(
            "Compact endpoint initialized.");

        Console.WriteLine();

        Console.WriteLine(
            $"Authoritative endpoint : {descriptor.Id.Value}");

        Console.WriteLine(
            $"Descriptor id          : "
            + $"{PhysicalArduinoUnoCompactDescriptorFactory.DescriptorReference.Id.Value}");

        Console.WriteLine(
            $"Descriptor version     : "
            + $"{PhysicalArduinoUnoCompactDescriptorFactory.DescriptorReference.Version}");

        Console.WriteLine(
            $"Display name           : "
            + $"{descriptor.Metadata.DisplayName}");

        Console.WriteLine(
            $"Instrument count       : "
            + $"{descriptor.Instruments.Count}");

        Console.WriteLine(
            $"Command path           : {command.Path}");

        Console.WriteLine(
            $"Command name           : {command.DisplayName}");

        Console.WriteLine(
            $"Compact command id     : "
            + $"0x{PhysicalArduinoUnoCompactDescriptorFactory.ToggleBuiltInLedCompactCommandId:X2}");

        Console.WriteLine(
            $"Connection state       : "
            + $"{connection.Connection.State}");

        Console.WriteLine();
    }

    private static void WriteCommandResult(
        CompactCommandExecutionStatus status)
    {
        Console.WriteLine(
            $"Command result         : {status}");

        Console.WriteLine();

        Console.WriteLine(
            status == CompactCommandExecutionStatus.Success
                ? "The Arduino Uno built-in LED should now be toggled."
                : "The Arduino Uno did not report successful command execution.");

        Console.WriteLine();

        Console.WriteLine(
            "The serial connection will now be closed.");
    }

    private static void WriteHeader(
        SerialTransportOptions transportOptions)
    {
        const string title =
            "Capability C-019";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Toggle the physical Arduino Uno built-in LED through the "
            + "Compact Serial Protocol.");

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
            $"Descriptor reference : "
            + $"{PhysicalArduinoUnoCompactDescriptorFactory.DescriptorReference.Id.Value} "
            + $"v{PhysicalArduinoUnoCompactDescriptorFactory.DescriptorReference.Version}");

        Console.WriteLine(
            "Compact command      : 0x01 - Toggle built-in LED");

        Console.WriteLine();
    }
}