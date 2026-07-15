using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.ProtocolExplorer.Transport;
using Hase.Transport;
using Hase.Transport.Tcp;

namespace Hase.ProtocolExplorer.Scenarios;

internal sealed class CapabilityC006Scenario
    : IParameterizedScenario
{
    private const int TcpPort =
        5000;

    private const int MaximumPayloadLength =
        4096;

    private const int ResponseWaitMilliseconds =
        2000;

    private const string InstrumentIdValue =
        "environment-sensor-01";

    private const string TemperaturePropertyIdValue =
        "physical.environment-sensor.temperature";

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

        WriteRequestInformation(
            requestEnvelope,
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

            using var cancellationSource =
                new CancellationTokenSource(
                    TimeSpan.FromMilliseconds(
                        ResponseWaitMilliseconds));

            try
            {
                byte[] unexpectedResponse =
                    await connection.ExchangeAsync(
                        requestFrame,
                        cancellationSource.Token);

                Console.WriteLine(
                    "The ESP32 returned a response before the "
                    + "temporary timeout.");

                Console.WriteLine(
                    $"Response Length : "
                    + $"{unexpectedResponse.Length} bytes");

                Console.WriteLine();

                throw new InvalidDataException(
                    "C-006 does not yet expect a "
                    + "ReadPropertyResponse.");
            }
            catch (OperationCanceledException)
                when (cancellationSource.IsCancellationRequested)
            {
                Console.WriteLine(
                    "Request frame was written.");

                Console.WriteLine(
                    "No response was expected at this checkpoint.");

                Console.WriteLine(
                    $"Wait expired after "
                    + $"{ResponseWaitMilliseconds} ms.");

                Console.WriteLine();
            }
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

        Console.WriteLine(
            "Capability Result");

        Console.WriteLine(
            "-----------------");

        Console.WriteLine();

        Console.WriteLine(
            "Request Transmission : Completed");

        Console.WriteLine(
            "Response Validation   : Deferred");

        Console.WriteLine(
            "Physical Validation   : Check ESP32 Serial Monitor");

        Console.WriteLine();
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
            "Send a physical BME280 temperature "
            + "ReadPropertyRequest over framed TCP.");

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

    private static void WriteRequestInformation(
        ProtocolEnvelope envelope,
        IReadOnlyList<byte> frame)
    {
        Console.WriteLine(
            "Read Property Request");

        Console.WriteLine(
            "---------------------");

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

        Console.WriteLine(
            $"Frame Length   : {frame.Count} bytes");

        Console.WriteLine();
    }
}