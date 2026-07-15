using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.ProtocolExplorer.Transport;
using Hase.Transport;
using Hase.Transport.Tcp;
using System.Diagnostics;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class CapabilityC005Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    private const string ExpectedEndpointId =
        "ideaspark-esp32-01";

    private const string ExpectedDisplayName =
        "Ideaspark ESP32 Environment Endpoint";

    private const string ExpectedDescription =
        "Physical HASE endpoint running on an Ideaspark ESP32 board.";

    private const string ExpectedInstrumentId =
        "environment-sensor-01";

    private const string ExpectedInstrumentName =
        "BMP280 Environment Sensor";

    private const string ExpectedInstrumentKind =
        "environment-sensor";

    private const string ExpectedManufacturer =
        "Bosch Sensortec";

    private const string ExpectedModel =
        "BMP280";

    private const string ExpectedInstrumentDescription =
        "Temperature and air-pressure sensor connected to the ESP32.";

    private static readonly CorrelationId
        DescriptorCorrelationId =
        new(105);

    public string Name =>
        "c005";

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
                "Capability C-005 requires exactly one argument: "
                + "the ESP32 host name or IP address.",
                nameof(arguments));
        }

        string host =
            arguments[0];

        WriteCapabilityHeader();

        Console.WriteLine(
            "TCP Endpoint");

        Console.WriteLine(
            "------------");

        Console.WriteLine();

        Console.WriteLine(
            $"Host : {host}");

        Console.WriteLine(
            $"Port : {TcpPort}");

        Console.WriteLine();

        var endpointId =
            new EndpointId(
                ExpectedEndpointId);

        var request =
            new ReadEndpointDescriptorRequest(
                DescriptorCorrelationId,
                endpointId);

        var payloadCodec =
            new BinaryProtocolPayloadCodec();

        ProtocolEnvelope requestEnvelope =
            payloadCodec.Encode(
                request);

        var envelopeByteCodec =
            new ProtocolEnvelopeByteCodec();

        byte[] requestFrame =
            envelopeByteCodec.Encode(
                requestEnvelope);

        WriteProtocolInformation(
            "Read Endpoint Descriptor Request",
            requestEnvelope);

        WriteBytes(
            "Encoded Request Frame",
            requestFrame);

        var options =
            new TcpTransportOptions(
                host,
                TcpPort);

        ITransportFactory factory =
            new TcpTransportFactory(
                options,
                MaximumPayloadLength);

        Console.WriteLine(
            "Establishing TCP connection...");

        ITransportConnection connection =
            await factory.ConnectAsync();

        try
        {
            Console.WriteLine(
                "TCP connection established.");

            Console.WriteLine();

            var stopwatch =
                Stopwatch.StartNew();

            byte[] responseFrame =
                await connection.ExchangeAsync(
                    requestFrame);

            stopwatch.Stop();

            WriteBytes(
                "Encoded Response Frame",
                responseFrame);

            ProtocolEnvelope responseEnvelope =
                envelopeByteCodec.Decode(
                    responseFrame);

            ProtocolMessage responseMessage =
                payloadCodec.Decode(
                    responseEnvelope);

            if (responseMessage
                is not ReadEndpointDescriptorResponse response)
            {
                throw new InvalidDataException(
                    "The ESP32 response did not decode as a "
                    + "ReadEndpointDescriptorResponse.");
            }

            ValidateResponse(
                response);

            WriteProtocolInformation(
                "Read Endpoint Descriptor Response",
                responseEnvelope);

            WriteDescriptorResult(
                response,
                stopwatch.Elapsed);
        }
        finally
        {
            if (connection is IAsyncDisposable
                asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (connection is IDisposable
                     disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static void ValidateResponse(
        ReadEndpointDescriptorResponse response)
    {
        if (response.CorrelationId
            != DescriptorCorrelationId)
        {
            throw new InvalidDataException(
                "The descriptor-response correlation identifier does "
                + "not match the descriptor request.");
        }

        if (!response.Result.IsSuccess)
        {
            throw new InvalidDataException(
                "The endpoint returned descriptor result "
                + $"'{response.Result.Code}': "
                + $"{response.Result.Message ?? "(no message)"}.");
        }

        EndpointDescriptor descriptor =
            response.Descriptor
            ?? throw new InvalidDataException(
                "The successful descriptor response did not contain "
                + "an endpoint descriptor.");

        ValidateEndpoint(
            descriptor);

        InstrumentDescriptor instrument =
            ValidateInstrument(
                descriptor);

        ValidateTemperatureProperty(
            instrument);

        ValidateAirPressureProperty(
            instrument);
    }

    private static void ValidateEndpoint(
        EndpointDescriptor descriptor)
    {
        var expectedEndpointId =
            new EndpointId(
                ExpectedEndpointId);

        if (descriptor.Id
            != expectedEndpointId)
        {
            throw new InvalidDataException(
                $"Expected endpoint '{ExpectedEndpointId}', but "
                + $"received '{descriptor.Id.Value}'.");
        }

        if (descriptor.Metadata.DisplayName
            != ExpectedDisplayName)
        {
            throw new InvalidDataException(
                $"Expected display name '{ExpectedDisplayName}', but "
                + $"received '{descriptor.Metadata.DisplayName}'.");
        }

        if (descriptor.Metadata.Description
            != ExpectedDescription)
        {
            throw new InvalidDataException(
                $"Expected description '{ExpectedDescription}', but "
                + $"received '{descriptor.Metadata.Description}'.");
        }

        if (descriptor.Instruments.Count
            != 1)
        {
            throw new InvalidDataException(
                "The physical endpoint descriptor must contain "
                + "exactly one instrument.");
        }
    }

    private static InstrumentDescriptor ValidateInstrument(
        EndpointDescriptor descriptor)
    {
        InstrumentDescriptor instrument =
            descriptor.Instruments[0];

        var expectedInstrumentId =
            new InstrumentId(
                ExpectedInstrumentId);

        if (instrument.Id
            != expectedInstrumentId)
        {
            throw new InvalidDataException(
                $"Expected instrument '{ExpectedInstrumentId}', but "
                + $"received '{instrument.Id.Value}'.");
        }

        if (instrument.Name
            != ExpectedInstrumentName)
        {
            throw new InvalidDataException(
                $"Expected instrument name '{ExpectedInstrumentName}', "
                + $"but received '{instrument.Name}'.");
        }

        if (instrument.Kind.Name
            != ExpectedInstrumentKind)
        {
            throw new InvalidDataException(
                $"Expected instrument kind '{ExpectedInstrumentKind}', "
                + $"but received '{instrument.Kind.Name}'.");
        }

        if (instrument.Metadata.Manufacturer
            != ExpectedManufacturer)
        {
            throw new InvalidDataException(
                $"Expected manufacturer '{ExpectedManufacturer}', but "
                + $"received '{instrument.Metadata.Manufacturer}'.");
        }

        if (instrument.Metadata.Model
            != ExpectedModel)
        {
            throw new InvalidDataException(
                $"Expected model '{ExpectedModel}', but received "
                + $"'{instrument.Metadata.Model}'.");
        }

        if (instrument.Metadata.SerialNumber is not null)
        {
            throw new InvalidDataException(
                "The current instrument descriptor must not contain "
                + "a serial number.");
        }

        if (instrument.Metadata.FirmwareVersion is not null)
        {
            throw new InvalidDataException(
                "The current instrument descriptor must not contain "
                + "a firmware version.");
        }

        if (instrument.Metadata.HardwareRevision is not null)
        {
            throw new InvalidDataException(
                "The current instrument descriptor must not contain "
                + "a hardware revision.");
        }

        if (instrument.Metadata.Description
            != ExpectedInstrumentDescription)
        {
            throw new InvalidDataException(
                $"Expected instrument description "
                + $"'{ExpectedInstrumentDescription}', but received "
                + $"'{instrument.Metadata.Description}'.");
        }

        if (instrument.Interface.Properties.Count
            != 2)
        {
            throw new InvalidDataException(
                "The physical BMP280 descriptor must contain exactly "
                + "two properties.");
        }

        if (instrument.Interface.Commands.Count
            != 0)
        {
            throw new InvalidDataException(
                "The physical BMP280 descriptor must not contain "
                + "commands.");
        }

        if (instrument.Interface.Events.Count
            != 0)
        {
            throw new InvalidDataException(
                "The physical BMP280 descriptor must not contain "
                + "events.");
        }

        return instrument;
    }

    private static void ValidateTemperatureProperty(
        InstrumentDescriptor instrument)
    {
        PropertyDescriptor property =
            instrument.Interface.Properties[0];

        ValidateNumericProperty(
            property,
            expectedId:
                "physical.environment-sensor.temperature",
            expectedPath:
                new DescriptorPath(
                    "Environment",
                    "Temperature"),
            expectedDisplayName:
                "Temperature",
            expectedDescription:
                "Ambient temperature.",
            expectedQuantityId:
                "temperature",
            expectedQuantityDisplayName:
                "Temperature",
            expectedUnitId:
                "celsius",
            expectedUnitDisplayName:
                "Degree Celsius",
            expectedUnitSymbol:
                "°C",
            expectedMinimum:
                -100.0,
            expectedMaximum:
                100.0,
            expectedResolution:
                0.1);
    }

    private static void ValidateAirPressureProperty(
        InstrumentDescriptor instrument)
    {
        PropertyDescriptor property =
            instrument.Interface.Properties[1];

        ValidateNumericProperty(
            property,
            expectedId:
                "physical.environment-sensor.air-pressure",
            expectedPath:
                new DescriptorPath(
                    "Environment",
                    "AirPressure"),
            expectedDisplayName:
                "Air Pressure",
            expectedDescription:
                "Ambient air pressure.",
            expectedQuantityId:
                "pressure",
            expectedQuantityDisplayName:
                "Pressure",
            expectedUnitId:
                "hectopascal",
            expectedUnitDisplayName:
                "Hectopascal",
            expectedUnitSymbol:
                "hPa",
            expectedMinimum:
                300.0,
            expectedMaximum:
                1100.0,
            expectedResolution:
                0.1);
    }

    private static void ValidateNumericProperty(
        PropertyDescriptor property,
        string expectedId,
        DescriptorPath expectedPath,
        string expectedDisplayName,
        string expectedDescription,
        string expectedQuantityId,
        string expectedQuantityDisplayName,
        string expectedUnitId,
        string expectedUnitDisplayName,
        string expectedUnitSymbol,
        double expectedMinimum,
        double expectedMaximum,
        double expectedResolution)
    {
        var expectedPropertyId =
            new PropertyId(
                expectedId);

        if (property.Id
            != expectedPropertyId)
        {
            throw new InvalidDataException(
                $"Expected property '{expectedId}', but received "
                + $"'{property.Id.Value}'.");
        }

        if (property.Path
            != expectedPath)
        {
            throw new InvalidDataException(
                $"Property '{expectedId}' has unexpected path "
                + $"'{property.Path}'.");
        }

        if (property.DisplayName
            != expectedDisplayName)
        {
            throw new InvalidDataException(
                $"Property '{expectedId}' has unexpected display name "
                + $"'{property.DisplayName}'.");
        }

        if (property.Description
            != expectedDescription)
        {
            throw new InvalidDataException(
                $"Property '{expectedId}' has unexpected description "
                + $"'{property.Description}'.");
        }

        if (property.AccessMode
            != PropertyAccessMode.Read)
        {
            throw new InvalidDataException(
                $"Property '{expectedId}' must be read-only.");
        }

        if (property.Data
            is not NumericDataDescriptor numericData)
        {
            throw new InvalidDataException(
                $"Property '{expectedId}' does not contain a numeric "
                + "data descriptor.");
        }

        if (numericData.Quantity.Id
            != expectedQuantityId
            || numericData.Quantity.DisplayName
            != expectedQuantityDisplayName)
        {
            throw new InvalidDataException(
                $"Property '{expectedId}' has unexpected quantity "
                + $"'{numericData.Quantity.Id}' / "
                + $"'{numericData.Quantity.DisplayName}'.");
        }

        if (numericData.NativeUnit.Id
            != expectedUnitId
            || numericData.NativeUnit.DisplayName
                != expectedUnitDisplayName
            || numericData.NativeUnit.Symbol
                != expectedUnitSymbol)
        {
            throw new InvalidDataException(
                $"Property '{expectedId}' has unexpected native unit "
                + $"'{numericData.NativeUnit.Id}' / "
                + $"'{numericData.NativeUnit.DisplayName}' / "
                + $"'{numericData.NativeUnit.Symbol}'.");
        }

        if (numericData.NativeUnit.Quantity
            != numericData.Quantity)
        {
            throw new InvalidDataException(
                $"Property '{expectedId}' has a unit associated with "
                + "the wrong quantity.");
        }

        ValueRange range =
            numericData.Range
            ?? throw new InvalidDataException(
                $"Property '{expectedId}' does not contain a range.");

        if (range.Minimum
                != expectedMinimum
            || range.Maximum
                != expectedMaximum)
        {
            throw new InvalidDataException(
                $"Property '{expectedId}' has unexpected range "
                + $"{range.Minimum} to {range.Maximum}.");
        }

        Resolution resolution =
            numericData.Resolution
            ?? throw new InvalidDataException(
                $"Property '{expectedId}' does not contain a "
                + "resolution.");

        if (resolution.Value
            != expectedResolution)
        {
            throw new InvalidDataException(
                $"Property '{expectedId}' has unexpected resolution "
                + $"{resolution.Value}.");
        }
    }

    private static void WriteCapabilityHeader()
    {
        const string title =
            "Capability C-005";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Read and decode the complete descriptor of a physical "
            + "BMP280 endpoint through HASE Protocol Version 1 over "
            + "framed TCP.");

        Console.WriteLine();
    }

    private static void WriteProtocolInformation(
        string title,
        ProtocolEnvelope envelope)
    {
        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '-',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            $"Version        : {envelope.Version}");

        Console.WriteLine(
            $"Role           : {envelope.Role}");

        Console.WriteLine(
            $"Message Type   : {envelope.MessageType}");

        Console.WriteLine(
            $"Correlation Id : {envelope.CorrelationId}");

        Console.WriteLine(
            $"Payload Length : {envelope.PayloadLength} bytes");

        Console.WriteLine();
    }

    private static void WriteBytes(
        string title,
        IReadOnlyList<byte> bytes)
    {
        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '-',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            $"Frame Length : {bytes.Count} bytes");

        Console.WriteLine();

        Console.Write(
            "Bytes        :");

        foreach (byte value in bytes)
        {
            Console.Write(
                $" {value:X2}");
        }

        Console.WriteLine();
        Console.WriteLine();
    }

    private static void WriteDescriptorResult(
        ReadEndpointDescriptorResponse response,
        TimeSpan elapsed)
    {
        EndpointDescriptor descriptor =
            response.Descriptor!;

        InstrumentDescriptor instrument =
            descriptor.Instruments[0];

        const string title =
            "Capability Result";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '-',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Result           : Success");

        Console.WriteLine(
            "Descriptor Read  : Passed");

        Console.WriteLine(
            $"Endpoint ID      : {descriptor.Id.Value}");

        Console.WriteLine(
            $"Instrument ID    : {instrument.Id.Value}");

        Console.WriteLine(
            $"Property Count   : "
            + $"{instrument.Interface.Properties.Count}");

        foreach (PropertyDescriptor property
                 in instrument.Interface.Properties)
        {
            var numericData =
                (NumericDataDescriptor)property.Data;

            Console.WriteLine(
                $"Property         : {property.DisplayName}");

            Console.WriteLine(
                $"  ID             : {property.Id.Value}");

            Console.WriteLine(
                $"  Path           : {property.Path}");

            Console.WriteLine(
                $"  Quantity       : "
                + $"{numericData.Quantity.DisplayName}");

            Console.WriteLine(
                $"  Native Unit    : "
                + $"{numericData.NativeUnit.DisplayName} "
                + $"({numericData.NativeUnit.Symbol})");

            Console.WriteLine(
                $"  Range          : "
                + $"{numericData.Range!.Minimum} to "
                + $"{numericData.Range.Maximum}");

            Console.WriteLine(
                $"  Resolution     : "
                + $"{numericData.Resolution!.Value}");
        }

        Console.WriteLine(
            $"Command Count    : "
            + $"{instrument.Interface.Commands.Count}");

        Console.WriteLine(
            $"Event Count      : "
            + $"{instrument.Interface.Events.Count}");

        Console.WriteLine(
            $"Round Trip Time  : "
            + $"{elapsed.TotalMilliseconds:0.000} ms");

        Console.WriteLine();
    }
}