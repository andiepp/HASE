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
/// Validates endpoint-confirmed compact property writing against a physical
/// Arduino Uno-class endpoint.
/// </summary>
internal sealed class CapabilityC022Scenario
    : IParameterizedScenario
{
    private const int DefaultBaudRate =
        115200;

    private static readonly EndpointId PhysicalEndpointId =
        new(
            "arduino-uno-01");

    public string Name =>
        "c022";

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
                "Capability C-022 requires a COM port and accepts an optional "
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

        await using var coordinator =
            new CompactRuntimeEndpointConnectionCoordinator(
                connectionFactory,
                transportOptions,
                propertyMap,
                runtimeEndpoint,
                new EndpointDescriptorCompatibilityValidator());

        WriteHeader(
            transportOptions);

        Console.WriteLine(
            "Connecting and synchronizing the compact endpoint.");

        Console.WriteLine();

        await coordinator.ConnectAsync();

        RuntimeProperty runtimeProperty =
            FindBuiltInLedStateProperty(
                runtimeEndpoint);

        WriteConnectionState(
            runtimeEndpoint,
            runtimeProperty);

        await WriteAndValidateAsync(
            coordinator,
            runtimeProperty,
            requestedValue: false,
            label:
                "First write");

        await WriteAndValidateAsync(
            coordinator,
            runtimeProperty,
            requestedValue: true,
            label:
                "Second write");

        await WriteAndValidateAsync(
            coordinator,
            runtimeProperty,
            requestedValue: false,
            label:
                "Third write");

        Console.WriteLine(
            "Compact property writing completed successfully.");

        Console.WriteLine();

        Console.WriteLine(
            "Observed transition    : Off -> On -> Off");

        Console.WriteLine(
            $"Runtime endpoints      : {runtimeContext.Endpoints.Count}");

        Console.WriteLine();

        Console.WriteLine(
            "The compact endpoint connection will now be closed.");
    }

    private static async Task WriteAndValidateAsync(
        CompactRuntimeEndpointConnectionCoordinator coordinator,
        RuntimeProperty runtimeProperty,
        bool requestedValue,
        string label)
    {
        CompactRuntimePropertyWriteResult result =
            await coordinator.WritePropertyAsync(
                PhysicalArduinoUnoCompactDescriptorFactory
                    .BuiltInLedStateCompactPropertyId,
                requestedValue);

        if (result.WriteStatus
            != CompactPropertyWriteStatus.Success)
        {
            throw new InvalidDataException(
                $"{label} returned compact property-write status "
                + $"'{result.WriteStatus}'.");
        }

        if (result.ConfirmationReadStatus
            != CompactPropertyReadStatus.Success)
        {
            throw new InvalidDataException(
                $"{label} returned compact confirmation-read status "
                + $"'{result.ConfirmationReadStatus?.ToString() ?? "None"}'.");
        }

        if (!result.CacheUpdated)
        {
            throw new InvalidDataException(
                $"{label} did not update the runtime property cache.");
        }

        if (!ReferenceEquals(
                runtimeProperty,
                result.RuntimeProperty))
        {
            throw new InvalidDataException(
                $"{label} returned a different runtime property instance.");
        }

        PropertyValue propertyValue =
            runtimeProperty.CurrentValue
            ?? throw new InvalidDataException(
                $"{label} did not produce a cached property value.");

        if (propertyValue.Value is not bool confirmedValue)
        {
            throw new InvalidDataException(
                $"{label} produced a non-Boolean cached property value.");
        }

        if (confirmedValue != requestedValue)
        {
            throw new InvalidDataException(
                $"{label} requested {FormatBoolean(requestedValue)} but the "
                + $"endpoint confirmed {FormatBoolean(confirmedValue)}.");
        }

        if (propertyValue.TimestampUtc.Offset
            != TimeSpan.Zero)
        {
            throw new InvalidDataException(
                $"{label} produced a non-UTC cache timestamp.");
        }

        if (propertyValue.Quality
            != PropertyQuality.Good)
        {
            throw new InvalidDataException(
                $"{label} produced property quality "
                + $"'{propertyValue.Quality}' instead of 'Good'.");
        }

        Console.WriteLine(
            $"{label,-22}: {FormatBoolean(requestedValue)}");

        Console.WriteLine(
            $"Write status          : {result.WriteStatus}");

        Console.WriteLine(
            $"Confirmation read     : {result.ConfirmationReadStatus}");

        Console.WriteLine(
            $"Cache updated         : {result.CacheUpdated}");

        Console.WriteLine(
            $"Confirmed cache       : {FormatBoolean(confirmedValue)}");

        Console.WriteLine(
            $"Timestamp UTC         : {propertyValue.TimestampUtc:O}");

        Console.WriteLine(
            $"Quality               : {propertyValue.Quality}");

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

        return controller.FindProperty(
                PhysicalArduinoUnoCompactDescriptorFactory
                    .BuiltInLedStatePropertyId)
            ?? throw new InvalidDataException(
                "The Arduino Uno runtime endpoint does not contain the "
                + "expected built-in LED state property.");
    }

    private static void WriteConnectionState(
        RuntimeEndpoint runtimeEndpoint,
        RuntimeProperty runtimeProperty)
    {
        Console.WriteLine(
            $"Authoritative endpoint : "
            + $"{runtimeEndpoint.Descriptor.Id.Value}");

        Console.WriteLine(
            $"Connection state       : "
            + $"{runtimeEndpoint.ConnectionStatus.State}");

        Console.WriteLine(
            $"Property path          : "
            + $"{runtimeProperty.Descriptor.Path}");

        Console.WriteLine(
            $"Property access        : "
            + $"{runtimeProperty.Descriptor.AccessMode}");

        Console.WriteLine(
            $"Initial cache          : "
            + $"{FormatCachedValue(runtimeProperty)}");

        Console.WriteLine();
    }

    private static string FormatCachedValue(
        RuntimeProperty runtimeProperty)
    {
        if (runtimeProperty.CurrentValue?.Value is bool value)
        {
            return FormatBoolean(
                value);
        }

        return runtimeProperty.CurrentValue is null
            ? "Empty"
            : runtimeProperty.CurrentValue.Value?.ToString()
                ?? "(null)";
    }

    private static string FormatBoolean(
        bool value)
    {
        return value
            ? "On"
            : "Off";
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
            "Capability C-022";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Write the physical Arduino Uno built-in LED state explicitly "
            + "and synchronize each endpoint-confirmed value into the runtime "
            + "property cache.");

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
            "Compact property     : 0x01 - Built-in LED state");

        Console.WriteLine(
            "Property access      : ReadWrite");

        Console.WriteLine(
            "Validation sequence  : Off -> On -> Off");

        Console.WriteLine(
            "Cache policy         : Update only from confirmation read");

        Console.WriteLine();
    }
}