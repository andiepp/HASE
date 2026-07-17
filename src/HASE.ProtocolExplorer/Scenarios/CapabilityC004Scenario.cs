using System.Diagnostics;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.ProtocolExplorer.Transport;
using Hase.Transport;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class CapabilityC004Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    private const string ExpectedEndpointId =
        "doit-esp32-devkitc-v4-01";

    private const string ExpectedEnvironmentSensorInstrumentId =
        "environment-sensor-01";

    private const string ExpectedControllerInstrumentId =
        "controller-01";

    private static readonly CorrelationId
        DiscoveryCorrelationId =
        new(
            104);

    public string Name =>
        "c004";

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
                "Capability C-004 requires exactly one argument: "
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

        var request =
            new DiscoverRequest(
                DiscoveryCorrelationId);

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
            "Discover Request",
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
                is not DiscoverResponse response)
            {
                throw new InvalidDataException(
                    "The ESP32 response did not decode as a "
                    + "DiscoverResponse.");
            }

            ValidateResponse(
                response);

            WriteProtocolInformation(
                "Discover Response",
                responseEnvelope);

            WriteDiscoveryResult(
                response,
                stopwatch.Elapsed);
        }
        finally
        {
            if (connection
                is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (connection
                     is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static void ValidateResponse(
        DiscoverResponse response)
    {
        if (response.CorrelationId
            != DiscoveryCorrelationId)
        {
            throw new InvalidDataException(
                "The DiscoverResponse correlation identifier does "
                + "not match the DiscoverRequest.");
        }

        var expectedEndpointId =
            new EndpointId(
                ExpectedEndpointId);

        if (response.EndpointId
            != expectedEndpointId)
        {
            throw new InvalidDataException(
                $"Expected endpoint '{ExpectedEndpointId}', but "
                + $"received '{response.EndpointId.Value}'.");
        }

        if (response.InstrumentIds.Count
            != 2)
        {
            throw new InvalidDataException(
                "The DiscoverResponse must contain exactly two "
                + "instrument identifiers.");
        }

        var expectedEnvironmentSensorInstrumentId =
            new InstrumentId(
                ExpectedEnvironmentSensorInstrumentId);

        if (response.InstrumentIds[0]
            != expectedEnvironmentSensorInstrumentId)
        {
            throw new InvalidDataException(
                "Expected first instrument "
                + $"'{ExpectedEnvironmentSensorInstrumentId}', but "
                + $"received '{response.InstrumentIds[0].Value}'.");
        }

        var expectedControllerInstrumentId =
            new InstrumentId(
                ExpectedControllerInstrumentId);

        if (response.InstrumentIds[1]
            != expectedControllerInstrumentId)
        {
            throw new InvalidDataException(
                "Expected second instrument "
                + $"'{ExpectedControllerInstrumentId}', but "
                + $"received '{response.InstrumentIds[1].Value}'.");
        }
    }

    private static void WriteCapabilityHeader()
    {
        const string title =
            "Capability C-004";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Discover the physical DOIT ESP32 DEVKITC V4 endpoint "
            + "through HASE Protocol Version 1 over framed TCP.");

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

        foreach (byte value
                 in bytes)
        {
            Console.Write(
                $" {value:X2}");
        }

        Console.WriteLine();

        Console.WriteLine();
    }

    private static void WriteDiscoveryResult(
        DiscoverResponse response,
        TimeSpan elapsed)
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
            "Result           : Success");

        Console.WriteLine(
            "Discovery        : Passed");

        Console.WriteLine(
            $"Endpoint ID      : {response.EndpointId.Value}");

        Console.WriteLine(
            $"Instrument Count : {response.InstrumentIds.Count}");

        foreach (InstrumentId instrumentId
                 in response.InstrumentIds)
        {
            Console.WriteLine(
                $"Instrument ID   : {instrumentId.Value}");
        }

        Console.WriteLine(
            $"Round Trip Time  : "
            + $"{elapsed.TotalMilliseconds:0.000} ms");

        Console.WriteLine();
    }
}