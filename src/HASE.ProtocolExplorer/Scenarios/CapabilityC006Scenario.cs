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

    private const double MinimumPlausibleTemperatureCelsius =
        -40.0;

    private const double MaximumPlausibleTemperatureCelsius =
        85.0;

    private static readonly TimeSpan
        MaximumTimestampDifference =
        TimeSpan.FromMinutes(
            2);

    private static readonly CorrelationId
        ReadCorrelationId =
        new(106);

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

        var request =
            new ReadPropertyRequest(
                ReadCorrelationId,
                new InstrumentId(
                    InstrumentIdValue),
                new PropertyId(
                    TemperaturePropertyIdValue));

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
            "Read Property Request",
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
                is not ReadPropertyResponse response)
            {
                throw new InvalidDataException(
                    "The ESP32 response did not decode as a "
                    + "ReadPropertyResponse.");
            }

            PropertyValue propertyValue =
                ValidateResponse(
                    response);

            WriteProtocolInformation(
                "Read Property Response",
                responseEnvelope);

            WriteCapabilityResult(
                propertyValue,
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

    private static PropertyValue ValidateResponse(
        ReadPropertyResponse response)
    {
        if (response.CorrelationId
            != ReadCorrelationId)
        {
            throw new InvalidDataException(
                "The property-response correlation identifier does "
                + "not match the property request.");
        }

        if (!response.Result.IsSuccess)
        {
            throw new InvalidDataException(
                "The endpoint returned property result "
                + $"'{response.Result.Code}': "
                + $"{response.Result.Message ?? "(no message)"}.");
        }

        PropertyValue propertyValue =
            response.PropertyValue
            ?? throw new InvalidDataException(
                "The successful property response did not contain "
                + "a property value.");

        if (propertyValue.Value
            is not double temperature)
        {
            string actualType =
                propertyValue.Value?.GetType().FullName
                ?? "null";

            throw new InvalidDataException(
                "The temperature property did not contain a double "
                + $"value. Actual type: '{actualType}'.");
        }

        if (double.IsNaN(
                temperature)
            || double.IsInfinity(
                temperature))
        {
            throw new InvalidDataException(
                "The temperature property contained a non-finite "
                + $"value: {temperature}.");
        }

        if (temperature
                < MinimumPlausibleTemperatureCelsius
            || temperature
                > MaximumPlausibleTemperatureCelsius)
        {
            throw new InvalidDataException(
                "The temperature property contained an implausible "
                + $"BME280 value: {temperature} degree Celsius.");
        }

        if (propertyValue.Quality
            != PropertyQuality.Good)
        {
            throw new InvalidDataException(
                "The temperature property quality must be Good, "
                + $"but was '{propertyValue.Quality}'.");
        }

        if (propertyValue.TimestampUtc.Offset
            != TimeSpan.Zero)
        {
            throw new InvalidDataException(
                "The property timestamp is not expressed in UTC.");
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
                "The property timestamp differs from the current "
                + "UTC time by "
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
            "Read a live BME280 temperature value from a physical "
            + "ESP32 endpoint through HASE Protocol Version 1 over "
            + "framed TCP.");

        Console.WriteLine();

        Console.WriteLine(
            $"Host          : {host}");

        Console.WriteLine(
            $"Port          : {TcpPort}");

        Console.WriteLine(
            $"Instrument ID : {InstrumentIdValue}");

        Console.WriteLine(
            $"Property ID   : {TemperaturePropertyIdValue}");

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

    private static void WriteCapabilityResult(
        PropertyValue propertyValue,
        TimeSpan elapsed)
    {
        double temperature =
            (double)propertyValue.Value!;

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
            "Property Read    : Passed");

        Console.WriteLine(
            $"Temperature      : {temperature:0.000} degree Celsius");

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
}