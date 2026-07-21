using System.Globalization;
using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Properties;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates compact property reading against a physical Arduino Uno-class
/// endpoint by reading the built-in LED state before and after toggling it.
/// </summary>
internal sealed class CapabilityC020Scenario
    : IParameterizedScenario
{
    private const int DefaultBaudRate =
        115200;

    public string Name =>
        "c020";

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
                "Capability C-020 requires a COM port and accepts an optional "
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

        PropertyDescriptor property =
            FindBuiltInLedStateProperty(
                connection.Descriptor);

        WriteConnectedEndpoint(
            connection,
            property);

        var reader =
            new CompactPropertyReader(
                connection.Connection,
                propertyMap);

        var commandExecutor =
            new CompactCommandExecutor(
                connection.Connection);

        CompactPropertyReadResult initialRead =
            await reader.ReadAsync(
                PhysicalArduinoUnoCompactDescriptorFactory
                    .BuiltInLedStateCompactPropertyId);

        bool initialState =
            RequireSuccessfulBooleanValue(
                initialRead,
                "initial");

        WritePropertyValue(
            "Initial LED state",
            initialRead,
            initialState);

        Console.WriteLine(
            "Executing compact LED toggle command.");

        Console.WriteLine();

        CompactCommandExecutionStatus commandStatus =
            await commandExecutor.ExecuteAsync(
                PhysicalArduinoUnoCompactDescriptorFactory
                    .ToggleBuiltInLedCompactCommandId);

        Console.WriteLine(
            $"Command result         : {commandStatus}");

        Console.WriteLine();

        if (commandStatus
            != CompactCommandExecutionStatus.Success)
        {
            throw new InvalidDataException(
                "The Arduino Uno did not report successful LED toggle "
                + "execution.");
        }

        CompactPropertyReadResult updatedRead =
            await reader.ReadAsync(
                PhysicalArduinoUnoCompactDescriptorFactory
                    .BuiltInLedStateCompactPropertyId);

        bool updatedState =
            RequireSuccessfulBooleanValue(
                updatedRead,
                "updated");

        WritePropertyValue(
            "Updated LED state",
            updatedRead,
            updatedState);

        if (updatedState == initialState)
        {
            throw new InvalidDataException(
                "The compact LED state did not change after successful toggle "
                + "command execution.");
        }

        Console.WriteLine(
            "Compact property validation completed successfully.");

        Console.WriteLine();

        Console.WriteLine(
            $"Observed transition    : "
            + $"{FormatBoolean(initialState)} -> "
            + $"{FormatBoolean(updatedState)}");

        Console.WriteLine();

        Console.WriteLine(
            "The serial connection will now be closed.");
    }

    private static PropertyDescriptor FindBuiltInLedStateProperty(
        EndpointDescriptor descriptor)
    {
        PropertyDescriptor? property =
            descriptor.Instruments
                .Where(
                    instrument =>
                        instrument.Id
                        == PhysicalArduinoUnoCompactDescriptorFactory
                            .ControllerInstrumentId)
                .SelectMany(
                    instrument =>
                        instrument.Interface.Properties)
                .FirstOrDefault(
                    candidate =>
                        candidate.Id
                        == PhysicalArduinoUnoCompactDescriptorFactory
                            .BuiltInLedStatePropertyId);

        if (property is null)
        {
            throw new InvalidDataException(
                "The materialized Arduino Uno descriptor does not contain "
                + "the expected built-in LED state property.");
        }

        return property;
    }

    private static bool RequireSuccessfulBooleanValue(
        CompactPropertyReadResult result,
        string readName)
    {
        if (result.Status
            != CompactPropertyReadStatus.Success)
        {
            throw new InvalidDataException(
                $"The {readName} compact property read returned "
                + $"'{result.Status}'.");
        }

        if (result.Value is null)
        {
            throw new InvalidDataException(
                $"The {readName} compact property read did not contain a "
                + "property value.");
        }

        if (result.Value.Value is not bool value)
        {
            throw new InvalidDataException(
                $"The {readName} compact property read did not produce a "
                + "Boolean value.");
        }

        return value;
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
        PropertyDescriptor property)
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
            $"Property path          : {property.Path}");

        Console.WriteLine(
            $"Property name          : {property.DisplayName}");

        Console.WriteLine(
            $"Compact property id    : "
            + $"0x{PhysicalArduinoUnoCompactDescriptorFactory.BuiltInLedStateCompactPropertyId:X2}");

        Console.WriteLine(
            $"Value encoding         : "
            + $"{CompactPropertyValueEncoding.Boolean}");

        Console.WriteLine(
            $"Connection state       : "
            + $"{connection.Connection.State}");

        Console.WriteLine();
    }

    private static void WritePropertyValue(
        string label,
        CompactPropertyReadResult result,
        bool value)
    {
        PropertyValue propertyValue =
            result.Value
            ?? throw new InvalidOperationException(
                "A successful compact property result must contain a value.");

        Console.WriteLine(
            $"{label,-22}: {FormatBoolean(value)}");

        Console.WriteLine(
            $"Read status            : {result.Status}");

        Console.WriteLine(
            $"Timestamp UTC          : "
            + $"{propertyValue.TimestampUtc:O}");

        Console.WriteLine(
            $"Quality                : {propertyValue.Quality}");

        Console.WriteLine();
    }

    private static string FormatBoolean(
        bool value)
    {
        return value
            ? "On"
            : "Off";
    }

    private static void WriteHeader(
        SerialTransportOptions transportOptions)
    {
        const string title =
            "Capability C-020";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Read the physical Arduino Uno built-in LED state through the "
            + "Compact Serial Protocol before and after toggling it.");

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
            "Compact property     : 0x01 - Built-in LED state");

        Console.WriteLine(
            "Compact command      : 0x01 - Toggle built-in LED");

        Console.WriteLine();
    }
}