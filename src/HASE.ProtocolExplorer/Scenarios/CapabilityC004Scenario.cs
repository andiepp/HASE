using System.Diagnostics;
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

    private static readonly CorrelationId
        DiscoveryCorrelationId =
        new(104);

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
            "Encoded Protocol Frame",
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

            byte[] echoedFrame =
                await connection.ExchangeAsync(
                    requestFrame);

            stopwatch.Stop();

            WriteBytes(
                "Echoed Protocol Frame",
                echoedFrame);

            if (!requestFrame.SequenceEqual(
                    echoedFrame))
            {
                throw new InvalidDataException(
                    "The protocol frame returned by the ESP32 does not "
                    + "match the transmitted frame.");
            }

            ProtocolEnvelope echoedEnvelope =
                envelopeByteCodec.Decode(
                    echoedFrame);

            ProtocolMessage echoedMessage =
                payloadCodec.Decode(
                    echoedEnvelope);

            if (echoedMessage is not DiscoverRequest
                echoedRequest)
            {
                throw new InvalidDataException(
                    "The echoed protocol frame did not decode as a "
                    + "DiscoverRequest.");
            }

            if (echoedRequest.CorrelationId
                != DiscoveryCorrelationId)
            {
                throw new InvalidDataException(
                    "The echoed DiscoverRequest has a different "
                    + "correlation identifier.");
            }

            WriteProtocolInformation(
                "Decoded Echo",
                echoedEnvelope);

            WriteResult(
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
            "Send a genuine HASE DiscoverRequest protocol envelope "
            + "to a physical ESP32-WROOM endpoint and validate the "
            + "unchanged echo.");

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

    private static void WriteResult(
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
            "Result                  : Success");

        Console.WriteLine(
            "Protocol Frame Echo     : Passed");

        Console.WriteLine(
            "DiscoverRequest Decode  : Passed");

        Console.WriteLine(
            $"Round Trip Time         : "
            + $"{elapsed.TotalMilliseconds:0.000} ms");

        Console.WriteLine();
    }
}
