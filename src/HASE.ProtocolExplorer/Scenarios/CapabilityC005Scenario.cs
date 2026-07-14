using System.Diagnostics;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.ProtocolExplorer.Transport;
using Hase.Transport;
using Hase.Transport.Tcp;

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

        if (response.Descriptor is null)
        {
            throw new InvalidDataException(
                "The successful descriptor response did not contain "
                + "an endpoint descriptor.");
        }

        var expectedEndpointId =
            new EndpointId(
                ExpectedEndpointId);

        if (response.Descriptor.Id
            != expectedEndpointId)
        {
            throw new InvalidDataException(
                $"Expected endpoint '{ExpectedEndpointId}', but "
                + $"received '{response.Descriptor.Id.Value}'.");
        }

        if (response.Descriptor.Metadata.DisplayName
            != ExpectedDisplayName)
        {
            throw new InvalidDataException(
                $"Expected display name '{ExpectedDisplayName}', but "
                + $"received "
                + $"'{response.Descriptor.Metadata.DisplayName}'.");
        }

        if (response.Descriptor.Metadata.Description
            != ExpectedDescription)
        {
            throw new InvalidDataException(
                $"Expected description '{ExpectedDescription}', but "
                + $"received "
                + $"'{response.Descriptor.Metadata.Description}'.");
        }

        if (response.Descriptor.Instruments.Count
            != 1)
        {
            throw new InvalidDataException(
                "The physical endpoint descriptor must contain "
                + "exactly one instrument.");
        }

        var instrument =
            response.Descriptor.Instruments[0];

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
            != 0)
        {
            throw new InvalidDataException(
                "The current physical instrument descriptor must not "
                + "contain properties.");
        }

        if (instrument.Interface.Commands.Count
            != 0)
        {
            throw new InvalidDataException(
                "The current physical instrument descriptor must not "
                + "contain commands.");
        }

        if (instrument.Interface.Events.Count
            != 0)
        {
            throw new InvalidDataException(
                "The current physical instrument descriptor must not "
                + "contain events.");
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
            "Read and decode the descriptor of a physical "
            + "ESP32-WROOM endpoint through HASE Protocol Version 1 "
            + "over framed TCP.");

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
        var instrument =
            response.Descriptor!.Instruments[0];

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
            $"Endpoint ID      : "
            + $"{response.Descriptor.Id.Value}");

        Console.WriteLine(
            $"Display Name     : "
            + $"{response.Descriptor.Metadata.DisplayName}");

        Console.WriteLine(
            $"Instrument Count : "
            + $"{response.Descriptor.Instruments.Count}");

        Console.WriteLine(
            $"Instrument ID    : "
            + $"{instrument.Id.Value}");

        Console.WriteLine(
            $"Instrument Name  : "
            + $"{instrument.Name}");

        Console.WriteLine(
            $"Instrument Kind  : "
            + $"{instrument.Kind.Name}");

        Console.WriteLine(
            $"Manufacturer     : "
            + $"{instrument.Metadata.Manufacturer}");

        Console.WriteLine(
            $"Model            : "
            + $"{instrument.Metadata.Model}");

        Console.WriteLine(
            $"Property Count   : "
            + $"{instrument.Interface.Properties.Count}");

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