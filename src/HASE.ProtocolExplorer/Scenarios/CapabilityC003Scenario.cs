using System.Diagnostics;
using Hase.Transport;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class CapabilityC003Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    private static readonly byte[] TestPayload =
    [
        0x48,
        0x41,
        0x53,
        0x45,
        0x00,
        0x01,
        0x02,
        0x03
    ];

    public string Name =>
        "c003";

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
                "Capability C-003 requires exactly one argument: "
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

            WritePayload(
                "Transmitted Payload",
                TestPayload);

            var stopwatch =
                Stopwatch.StartNew();

            byte[] response =
                await connection.ExchangeAsync(
                    TestPayload);

            stopwatch.Stop();

            WritePayload(
                "Received Payload",
                response);

            if (!TestPayload.SequenceEqual(
                    response))
            {
                throw new InvalidDataException(
                    "The payload returned by the ESP32 does not match "
                    + "the transmitted payload.");
            }

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
            "Capability C-003";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Demonstrate framed TCP communication with a physical "
            + "ESP32-WROOM endpoint.");

        Console.WriteLine();
    }

    private static void WritePayload(
        string title,
        IReadOnlyList<byte> payload)
    {
        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '-',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            $"Payload Length : {payload.Count} bytes");

        Console.WriteLine();

        Console.Write(
            "Bytes          :");

        foreach (byte value in payload)
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
            "Result          : Success");

        Console.WriteLine(
            "Echo Validation : Passed");

        Console.WriteLine(
            $"Round Trip Time : {elapsed.TotalMilliseconds:0.000} ms");

        Console.WriteLine();
    }
}