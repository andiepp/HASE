using System.Diagnostics;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.ProtocolExplorer.Transport;
using Hase.Runtime.Transport;
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

    private static readonly CorrelationId
        DescriptorCorrelationId =
        new(
            105);

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

        EndpointDescriptor expectedDescriptor =
            PhysicalEnvironmentEndpointDescriptorFactory.Create();

        WriteCapabilityHeader(
            host,
            expectedDescriptor);

        var request =
            new ReadEndpointDescriptorRequest(
                DescriptorCorrelationId,
                expectedDescriptor.Id);

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

            EndpointDescriptor physicalDescriptor =
                ValidateResponse(
                    response,
                    expectedDescriptor);

            WriteProtocolInformation(
                "Read Endpoint Descriptor Response",
                responseEnvelope);

            WriteDescriptorResult(
                physicalDescriptor,
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

    private static EndpointDescriptor ValidateResponse(
        ReadEndpointDescriptorResponse response,
        EndpointDescriptor expectedDescriptor)
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

        EndpointDescriptor physicalDescriptor =
            response.Descriptor
            ?? throw new InvalidDataException(
                "The successful descriptor response did not contain "
                + "an endpoint descriptor.");

        var compatibilityValidator =
            new EndpointDescriptorCompatibilityValidator();

        compatibilityValidator.Validate(
            expectedDescriptor,
            physicalDescriptor);

        return physicalDescriptor;
    }

    private static void WriteCapabilityHeader(
        string host,
        EndpointDescriptor expectedDescriptor)
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
            "Read and strictly validate the complete descriptor of "
            + "the physical DOIT ESP32 DEVKITC V4 endpoint through "
            + "HASE Protocol Version 1 over framed TCP.");

        Console.WriteLine();

        Console.WriteLine(
            $"Host        : {host}");

        Console.WriteLine(
            $"Port        : {TcpPort}");

        Console.WriteLine(
            $"Endpoint ID : {expectedDescriptor.Id.Value}");

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

    private static void WriteDescriptorResult(
        EndpointDescriptor descriptor,
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
            "Descriptor Read  : Passed");

        Console.WriteLine(
            "Compatibility    : Passed");

        Console.WriteLine(
            $"Endpoint ID      : {descriptor.Id.Value}");

        Console.WriteLine(
            $"Endpoint Name    : "
            + $"{descriptor.Metadata.DisplayName}");

        Console.WriteLine(
            $"Instrument Count : {descriptor.Instruments.Count}");

        Console.WriteLine();

        foreach (InstrumentDescriptor instrument
                 in descriptor.Instruments)
        {
            WriteInstrument(
                instrument);
        }

        Console.WriteLine(
            $"Round Trip Time  : "
            + $"{elapsed.TotalMilliseconds:0.000} ms");

        Console.WriteLine();
    }

    private static void WriteInstrument(
        InstrumentDescriptor instrument)
    {
        Console.WriteLine(
            $"Instrument       : {instrument.Name}");

        Console.WriteLine(
            $"  ID             : {instrument.Id.Value}");

        Console.WriteLine(
            $"  Kind           : {instrument.Kind.Name}");

        Console.WriteLine(
            $"  Manufacturer   : "
            + $"{instrument.Metadata.Manufacturer ?? "<none>"}");

        Console.WriteLine(
            $"  Model          : "
            + $"{instrument.Metadata.Model ?? "<none>"}");

        Console.WriteLine(
            $"  Property Count : "
            + $"{instrument.Interface.Properties.Count}");

        foreach (PropertyDescriptor property
                 in instrument.Interface.Properties)
        {
            WriteProperty(
                property);
        }

        Console.WriteLine(
            $"  Command Count  : "
            + $"{instrument.Interface.Commands.Count}");

        Console.WriteLine(
            $"  Event Count    : "
            + $"{instrument.Interface.Events.Count}");

        Console.WriteLine();
    }

    private static void WriteProperty(
        PropertyDescriptor property)
    {
        Console.WriteLine(
            $"  Property       : {property.DisplayName}");

        Console.WriteLine(
            $"    ID           : {property.Id.Value}");

        Console.WriteLine(
            $"    Path         : {property.Path}");

        Console.WriteLine(
            $"    Access       : {property.AccessMode}");

        switch (property.Data)
        {
            case NumericDataDescriptor numericData:
                {
                    Console.WriteLine(
                        "    Data Type    : Numeric");

                    Console.WriteLine(
                        $"    Quantity     : "
                        + $"{numericData.Quantity.DisplayName}");

                    Console.WriteLine(
                        $"    Native Unit  : "
                        + $"{numericData.NativeUnit.DisplayName} "
                        + $"({numericData.NativeUnit.Symbol})");

                    if (numericData.Range is not null)
                    {
                        Console.WriteLine(
                            $"    Range        : "
                            + $"{numericData.Range.Minimum} to "
                            + $"{numericData.Range.Maximum}");
                    }

                    if (numericData.Resolution is not null)
                    {
                        Console.WriteLine(
                            $"    Resolution   : "
                            + $"{numericData.Resolution.Value}");
                    }

                    break;
                }

            case BooleanDataDescriptor:
                {
                    Console.WriteLine(
                        "    Data Type    : Boolean");

                    break;
                }

            case StringDataDescriptor:
                {
                    Console.WriteLine(
                        "    Data Type    : String");

                    break;
                }

            default:
                {
                    Console.WriteLine(
                        $"    Data Type    : "
                        + $"{property.Data.GetType().Name}");

                    break;
                }
        }
    }
}