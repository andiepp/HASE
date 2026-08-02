using System.Globalization;
using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Validates compact property synchronization against a physical Arduino
/// Uno-class endpoint by reading the runtime-cached built-in LED state before
/// and after toggling it.
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

        var runtimeContext =
            new RuntimeContext();

        RuntimeEndpoint runtimeEndpoint =
            runtimeContext.AddEndpoint(
                connection.Descriptor);

        RuntimeProperty runtimeProperty =
            FindBuiltInLedStateProperty(
                runtimeEndpoint);

        WriteConnectedEndpoint(
            connection,
            runtimeProperty);

        var synchronizer =
            new CompactRuntimePropertySynchronizer(
                connection.Connection,
                propertyMap);

        var commandExecutor =
            new CompactCommandExecutor(
                connection.Connection);

        IReadOnlyList<CompactRuntimePropertySynchronizationResult>
            initialResults =
            await synchronizer.SynchronizeAsync(
                runtimeEndpoint);

        CompactRuntimePropertySynchronizationResult initialResult =
            SelectResult(
                initialResults,
                PhysicalArduinoUnoCompactDescriptorFactory
                    .BuiltInLedStatePropertyId);

        bool initialState =
            RequireCachedBooleanValue(
                initialResult,
                "initial");

        WriteCachedPropertyValue(
            "Initial cached state",
            initialResult,
            initialState);

        WriteAnalogVoltage(
            "Initial A0 voltage",
            SelectResult(
                initialResults,
                PhysicalArduinoUnoCompactDescriptorFactory
                    .AnalogInputVoltagePropertyId));

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

        IReadOnlyList<CompactRuntimePropertySynchronizationResult>
            updatedResults =
            await synchronizer.SynchronizeAsync(
                runtimeEndpoint);

        CompactRuntimePropertySynchronizationResult updatedResult =
            SelectResult(
                updatedResults,
                PhysicalArduinoUnoCompactDescriptorFactory
                    .BuiltInLedStatePropertyId);

        bool updatedState =
            RequireCachedBooleanValue(
                updatedResult,
                "updated");

        WriteCachedPropertyValue(
            "Updated cached state",
            updatedResult,
            updatedState);

        WriteAnalogVoltage(
            "Updated A0 voltage",
            SelectResult(
                updatedResults,
                PhysicalArduinoUnoCompactDescriptorFactory
                    .AnalogInputVoltagePropertyId));

        if (updatedState == initialState)
        {
            throw new InvalidDataException(
                "The runtime-cached LED state did not change after successful "
                + "toggle command execution.");
        }

        Console.WriteLine(
            "Compact runtime property synchronization completed successfully.");

        Console.WriteLine();

        Console.WriteLine(
            $"Observed transition    : "
            + $"{FormatBoolean(initialState)} -> "
            + $"{FormatBoolean(updatedState)}");

        Console.WriteLine();

        Console.WriteLine(
            $"Runtime endpoints      : {runtimeContext.Endpoints.Count}");

        Console.WriteLine(
            $"Cache property         : "
            + $"{runtimeProperty.Descriptor.Path}");

        Console.WriteLine();

        Console.WriteLine(
            "The serial connection will now be closed.");
    }

    private static CompactRuntimePropertySynchronizationResult SelectResult(
        IReadOnlyList<CompactRuntimePropertySynchronizationResult> results,
        PropertyId propertyId)
    {
        return results.Single(result =>
            result.RuntimeProperty.Descriptor.Id == propertyId);
    }

    private static void WriteAnalogVoltage(
        string label,
        CompactRuntimePropertySynchronizationResult result)
    {
        if (result.Status != CompactPropertyReadStatus.Success
            || !result.CacheUpdated)
        {
            throw new InvalidDataException(
                "The A0 voltage synchronization did not update the cache.");
        }

        PropertyValue propertyValue =
            result.RuntimeProperty.CurrentValue
            ?? throw new InvalidDataException(
                "The A0 voltage synchronization did not produce a value.");
        double voltage =
            propertyValue.Value is double value
                ? value
                : throw new InvalidDataException(
                    "The synchronized A0 voltage is not numeric.");

        if (voltage is < 0.0 or > 5.0)
        {
            throw new InvalidDataException(
                "The synchronized A0 voltage is outside its descriptor range.");
        }

        Console.WriteLine($"{label,-22}: {voltage:F3} V");
        Console.WriteLine($"Read status            : {result.Status}");
        Console.WriteLine($"Cache updated          : {result.CacheUpdated}");
        Console.WriteLine();
    }

    private static RuntimeProperty FindBuiltInLedStateProperty(
        RuntimeEndpoint runtimeEndpoint)
    {
        RuntimeInstrument? controller =
            runtimeEndpoint.FindInstrument(
                PhysicalArduinoUnoCompactDescriptorFactory
                    .ControllerInstrumentId);

        if (controller is null)
        {
            throw new InvalidDataException(
                "The Arduino Uno runtime endpoint does not contain the "
                + "expected GPIO controller instrument.");
        }

        RuntimeProperty? property =
            controller.FindProperty(
                PhysicalArduinoUnoCompactDescriptorFactory
                    .BuiltInLedStatePropertyId);

        if (property is null)
        {
            throw new InvalidDataException(
                "The Arduino Uno runtime endpoint does not contain the "
                + "expected built-in LED state property.");
        }

        return property;
    }

    private static bool RequireCachedBooleanValue(
        CompactRuntimePropertySynchronizationResult result,
        string synchronizationName)
    {
        if (result.Status
            != CompactPropertyReadStatus.Success)
        {
            throw new InvalidDataException(
                $"The {synchronizationName} compact property synchronization "
                + $"returned '{result.Status}'.");
        }

        if (!result.CacheUpdated)
        {
            throw new InvalidDataException(
                $"The {synchronizationName} compact property synchronization "
                + "did not update the runtime cache.");
        }

        PropertyValue propertyValue =
            result.RuntimeProperty.CurrentValue
            ?? throw new InvalidDataException(
                $"The {synchronizationName} compact property synchronization "
                + "did not produce a cached property value.");

        if (propertyValue.Value is not bool value)
        {
            throw new InvalidDataException(
                $"The {synchronizationName} runtime cache value is not "
                + "Boolean.");
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
        RuntimeProperty runtimeProperty)
    {
        EndpointDescriptor descriptor =
            connection.Descriptor;

        Console.WriteLine(
            "Compact endpoint initialized and published in the runtime.");

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
            $"Property path          : "
            + $"{runtimeProperty.Descriptor.Path}");

        Console.WriteLine(
            $"Property name          : "
            + $"{runtimeProperty.Descriptor.DisplayName}");

        Console.WriteLine(
            $"Compact property id    : "
            + $"0x{PhysicalArduinoUnoCompactDescriptorFactory.BuiltInLedStateCompactPropertyId:X2}");

        Console.WriteLine(
            $"Value encoding         : "
            + $"{CompactPropertyValueEncoding.Boolean}");

        Console.WriteLine(
            $"Initial cache          : "
            + $"{(runtimeProperty.CurrentValue is null ? "Empty" : "Populated")}");

        Console.WriteLine(
            $"Connection state       : "
            + $"{connection.Connection.State}");

        Console.WriteLine();
    }

    private static void WriteCachedPropertyValue(
        string label,
        CompactRuntimePropertySynchronizationResult result,
        bool value)
    {
        PropertyValue propertyValue =
            result.RuntimeProperty.CurrentValue
            ?? throw new InvalidOperationException(
                "A successful synchronization must populate the cache.");

        Console.WriteLine(
            $"{label,-22}: {FormatBoolean(value)}");

        Console.WriteLine(
            $"Read status            : {result.Status}");

        Console.WriteLine(
            $"Cache updated          : {result.CacheUpdated}");

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
            "Synchronize the physical Arduino Uno built-in LED state into "
            + "the runtime property cache before and after toggling it.");

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

        Console.WriteLine(
            "Runtime cache        : Synchronize successful reads");

        Console.WriteLine();
    }
}
