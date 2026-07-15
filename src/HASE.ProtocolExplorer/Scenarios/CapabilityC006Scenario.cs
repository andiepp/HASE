using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.ProtocolExplorer.Transport;
using Hase.Transport;
using Hase.Transport.Tcp;
using System.Diagnostics;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class CapabilityC006Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    private const string InstrumentIdValue =
        "environment-sensor-01";

    private const string TemperaturePropertyIdValue =
        "physical.environment-sensor.temperature";

    private const string RelativeHumidityPropertyIdValue =
        "physical.environment-sensor.relative-humidity";

    private const string AirPressurePropertyIdValue =
        "physical.environment-sensor.air-pressure";

    private const double MinimumPlausibleTemperatureCelsius =
        -40.0;

    private const double MaximumPlausibleTemperatureCelsius =
        85.0;

    private const double MinimumPlausibleRelativeHumidity =
        0.0;

    private const double MaximumPlausibleRelativeHumidity =
        100.0;

    private const double MinimumPlausibleAirPressureHectopascal =
        300.0;

    private const double MaximumPlausibleAirPressureHectopascal =
        1100.0;

    private static readonly TimeSpan
        MaximumTimestampDifference =
        TimeSpan.FromMinutes(
            2);

    private static readonly CorrelationId
        TemperatureCorrelationId =
        new(106);

    private static readonly CorrelationId
        RelativeHumidityCorrelationId =
        new(107);

    private static readonly CorrelationId
        AirPressureCorrelationId =
        new(108);

    public string Name =>
        "c006";

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
                "Capability C-006 requires exactly one argument: "
                + "the ESP32 host name or IP address.",
                nameof(arguments));
        }

        string host =
            arguments[0];

        WriteCapabilityHeader(
            host);

        var payloadCodec =
            new BinaryProtocolPayloadCodec();

        var envelopeByteCodec =
            new ProtocolEnvelopeByteCodec();

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

            PropertyReadResult temperature =
                await ReadPropertyAsync(
                    connection,
                    payloadCodec,
                    envelopeByteCodec,
                    TemperatureCorrelationId,
                    TemperaturePropertyIdValue,
                    "Temperature",
                    MinimumPlausibleTemperatureCelsius,
                    MaximumPlausibleTemperatureCelsius,
                    "degree Celsius");

            PropertyReadResult relativeHumidity =
                await ReadPropertyAsync(
                    connection,
                    payloadCodec,
                    envelopeByteCodec,
                    RelativeHumidityCorrelationId,
                    RelativeHumidityPropertyIdValue,
                    "Relative Humidity",
                    MinimumPlausibleRelativeHumidity,
                    MaximumPlausibleRelativeHumidity,
                    "%RH");

            PropertyReadResult airPressure =
                await ReadPropertyAsync(
                    connection,
                    payloadCodec,
                    envelopeByteCodec,
                    AirPressureCorrelationId,
                    AirPressurePropertyIdValue,
                    "Air Pressure",
                    MinimumPlausibleAirPressureHectopascal,
                    MaximumPlausibleAirPressureHectopascal,
                    "hPa");

            WriteCapabilityResult(
                temperature,
                relativeHumidity,
                airPressure);
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

    private static async Task<PropertyReadResult>
        ReadPropertyAsync(
            ITransportConnection connection,
            BinaryProtocolPayloadCodec payloadCodec,
            ProtocolEnvelopeByteCodec envelopeByteCodec,
            CorrelationId correlationId,
            string propertyIdValue,
            string displayName,
            double minimumPlausibleValue,
            double maximumPlausibleValue,
            string unitSymbol)
    {
        var request =
            new ReadPropertyRequest(
                correlationId,
                new InstrumentId(
                    InstrumentIdValue),
                new PropertyId(
                    propertyIdValue));

        ProtocolEnvelope requestEnvelope =
            payloadCodec.Encode(
                request);

        byte[] requestFrame =
            envelopeByteCodec.Encode(
                requestEnvelope);

        WritePropertyHeader(
            displayName,
            propertyIdValue);

        WriteProtocolInformation(
            "Read Property Request",
            requestEnvelope);

        WriteBytes(
            "Encoded Request Frame",
            requestFrame);

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
            is not ReadPropertyResponse response)
        {
            throw new InvalidDataException(
                $"The ESP32 response for '{displayName}' did not "
                + "decode as a ReadPropertyResponse.");
        }

        PropertyValue propertyValue =
            ValidateResponse(
                response,
                correlationId,
                displayName,
                minimumPlausibleValue,
                maximumPlausibleValue,
                unitSymbol);

        WriteProtocolInformation(
            "Read Property Response",
            responseEnvelope);

        WritePropertyResult(
            displayName,
            propertyIdValue,
            propertyValue,
            unitSymbol,
            stopwatch.Elapsed);

        return new PropertyReadResult(
            displayName,
            propertyIdValue,
            (double)propertyValue.Value!,
            propertyValue.TimestampUtc,
            propertyValue.Quality,
            unitSymbol,
            stopwatch.Elapsed);
    }

    private static PropertyValue ValidateResponse(
        ReadPropertyResponse response,
        CorrelationId expectedCorrelationId,
        string displayName,
        double minimumPlausibleValue,
        double maximumPlausibleValue,
        string unitSymbol)
    {
        if (response.CorrelationId
            != expectedCorrelationId)
        {
            throw new InvalidDataException(
                $"The '{displayName}' response correlation identifier "
                + "does not match its request.");
        }

        if (!response.Result.IsSuccess)
        {
            throw new InvalidDataException(
                $"The endpoint returned '{displayName}' result "
                + $"'{response.Result.Code}': "
                + $"{response.Result.Message ?? "(no message)"}.");
        }

        PropertyValue propertyValue =
            response.PropertyValue
            ?? throw new InvalidDataException(
                $"The successful '{displayName}' response did not "
                + "contain a property value.");

        if (propertyValue.Value
            is not double value)
        {
            string actualType =
                propertyValue.Value?.GetType().FullName
                ?? "null";

            throw new InvalidDataException(
                $"The '{displayName}' property did not contain a "
                + $"double value. Actual type: '{actualType}'.");
        }

        if (double.IsNaN(
                value)
            || double.IsInfinity(
                value))
        {
            throw new InvalidDataException(
                $"The '{displayName}' property contained a non-finite "
                + $"value: {value}.");
        }

        if (value
                < minimumPlausibleValue
            || value
                > maximumPlausibleValue)
        {
            throw new InvalidDataException(
                $"The '{displayName}' property contained an "
                + $"implausible BME280 value: {value} {unitSymbol}. "
                + $"Expected range: {minimumPlausibleValue} to "
                + $"{maximumPlausibleValue} {unitSymbol}.");
        }

        if (propertyValue.Quality
            != PropertyQuality.Good)
        {
            throw new InvalidDataException(
                $"The '{displayName}' property quality must be Good, "
                + $"but was '{propertyValue.Quality}'.");
        }

        if (propertyValue.TimestampUtc.Offset
            != TimeSpan.Zero)
        {
            throw new InvalidDataException(
                $"The '{displayName}' timestamp is not expressed "
                + "in UTC.");
        }

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        TimeSpan timestampDifference =
            (now - propertyValue.TimestampUtc)
            .Duration();

        if (timestampDifference
            > MaximumTimestampDifference)
        {
            throw new InvalidDataException(
                $"The '{displayName}' timestamp differs from the "
                + "current UTC time by "
                + $"{timestampDifference.TotalSeconds:0.000} seconds.");
        }

        return propertyValue;
    }

    private static void WriteCapabilityHeader(
        string host)
    {
        const string title =
            "Capability C-006";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Read live BME280 property values from a physical ESP32 "
            + "endpoint through HASE Protocol Version 1 over framed "
            + "TCP.");

        Console.WriteLine();

        Console.WriteLine(
            $"Host          : {host}");

        Console.WriteLine(
            $"Port          : {TcpPort}");

        Console.WriteLine(
            $"Instrument ID : {InstrumentIdValue}");

        Console.WriteLine(
            "Properties    :");

        Console.WriteLine(
            $"  {TemperaturePropertyIdValue}");

        Console.WriteLine(
            $"  {RelativeHumidityPropertyIdValue}");

        Console.WriteLine(
            $"  {AirPressurePropertyIdValue}");

        Console.WriteLine();
    }

    private static void WritePropertyHeader(
        string displayName,
        string propertyIdValue)
    {
        string title =
            $"Physical Property: {displayName}";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            $"Property ID : {propertyIdValue}");

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
            $"Correlation ID : {envelope.CorrelationId}");

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

    private static void WritePropertyResult(
        string displayName,
        string propertyIdValue,
        PropertyValue propertyValue,
        string unitSymbol,
        TimeSpan elapsed)
    {
        double value =
            (double)propertyValue.Value!;

        const string title =
            "Property Result";

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
            $"Property         : {displayName}");

        Console.WriteLine(
            $"Property ID      : {propertyIdValue}");

        Console.WriteLine(
            $"Value            : {value:0.000} {unitSymbol}");

        Console.WriteLine(
            $"Timestamp UTC    : "
            + $"{propertyValue.TimestampUtc:O}");

        Console.WriteLine(
            $"Quality          : {propertyValue.Quality}");

        Console.WriteLine(
            $"Round Trip Time  : "
            + $"{elapsed.TotalMilliseconds:0.000} ms");

        Console.WriteLine();
    }

    private static void WriteCapabilityResult(
        PropertyReadResult temperature,
        PropertyReadResult relativeHumidity,
        PropertyReadResult airPressure)
    {
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
            "Result              : Success");

        Console.WriteLine(
            "Physical Reads      : Passed");

        Console.WriteLine(
            $"Temperature         : "
            + $"{temperature.Value:0.000} "
            + $"{temperature.UnitSymbol}");

        Console.WriteLine(
            $"Relative Humidity   : "
            + $"{relativeHumidity.Value:0.000} "
            + $"{relativeHumidity.UnitSymbol}");

        Console.WriteLine(
            $"Air Pressure        : "
            + $"{airPressure.Value:0.000} "
            + $"{airPressure.UnitSymbol}");

        Console.WriteLine(
            $"Temperature Quality : {temperature.Quality}");

        Console.WriteLine(
            $"Humidity Quality    : {relativeHumidity.Quality}");

        Console.WriteLine(
            $"Pressure Quality    : {airPressure.Quality}");

        Console.WriteLine(
            $"Temperature UTC     : "
            + $"{temperature.TimestampUtc:O}");

        Console.WriteLine(
            $"Humidity UTC        : "
            + $"{relativeHumidity.TimestampUtc:O}");

        Console.WriteLine(
            $"Pressure UTC        : "
            + $"{airPressure.TimestampUtc:O}");

        double totalRoundTripMilliseconds =
            temperature.Elapsed.TotalMilliseconds
            + relativeHumidity.Elapsed.TotalMilliseconds
            + airPressure.Elapsed.TotalMilliseconds;

        Console.WriteLine(
            $"Total Round Trip    : "
            + $"{totalRoundTripMilliseconds:0.000} ms");

        Console.WriteLine();
    }

    private sealed record PropertyReadResult(
        string DisplayName,
        string PropertyId,
        double Value,
        DateTimeOffset TimestampUtc,
        PropertyQuality Quality,
        string UnitSymbol,
        TimeSpan Elapsed);
}