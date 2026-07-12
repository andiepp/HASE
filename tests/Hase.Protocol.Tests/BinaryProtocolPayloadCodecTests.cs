using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests;

public sealed class BinaryProtocolPayloadCodecTests
{
    private sealed record UnsupportedTestMessage(
        CorrelationId CorrelationId)
        : ProtocolMessage(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.ReadPropertyRequest,
            CorrelationId);

    [Fact]
    public void Encode_DiscoverRequest_CreatesExpectedEnvelope()
    {
        BinaryProtocolPayloadCodec codec = new();

        DiscoverRequest request =
            new(new CorrelationId(17));

        ProtocolEnvelope envelope =
            codec.Encode(request);

        Assert.Equal(
            ProtocolVersion.Current,
            envelope.Version);

        Assert.Equal(
            ProtocolMessageRole.Request,
            envelope.Role);

        Assert.Equal(
            ProtocolMessageType.DiscoverRequest,
            envelope.MessageType);

        Assert.Equal(
            new CorrelationId(17),
            envelope.CorrelationId);

        Assert.True(
            envelope.Payload.IsEmpty);
    }

    [Fact]
    public void Decode_DiscoverRequest_CreatesExpectedMessage()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            new CorrelationId(17),
            ReadOnlyMemory<byte>.Empty);

        DiscoverRequest request =
            Assert.IsType<DiscoverRequest>(
                codec.Decode(envelope));

        Assert.Equal(
            new CorrelationId(17),
            request.CorrelationId);
    }

    [Fact]
    public void Encode_DiscoverResponse_WritesExpectedPayload()
    {
        BinaryProtocolPayloadCodec codec = new();

        DiscoverResponse response = new(
            new CorrelationId(7),
            new EndpointId("EP1"),
            new[]
            {
                new InstrumentId("I1"),
                new InstrumentId("I2")
            });

        ProtocolEnvelope envelope =
            codec.Encode(response);

        Assert.Equal(
            ProtocolVersion.Current,
            envelope.Version);

        Assert.Equal(
            ProtocolMessageRole.Response,
            envelope.Role);

        Assert.Equal(
            ProtocolMessageType.DiscoverResponse,
            envelope.MessageType);

        Assert.Equal(
            new CorrelationId(7),
            envelope.CorrelationId);

        Assert.Equal(
            new byte[]
            {
                0x03, 0x00,
                (byte)'E', (byte)'P', (byte)'1',

                0x02, 0x00,

                0x02, 0x00,
                (byte)'I', (byte)'1',

                0x02, 0x00,
                (byte)'I', (byte)'2'
            },
            envelope.Payload.ToArray());
    }

    [Fact]
    public void Decode_DiscoverResponse_ReadsExpectedMessage()
    {
        BinaryProtocolPayloadCodec codec = new();

        byte[] payload =
        {
            0x03, 0x00,
            (byte)'E', (byte)'P', (byte)'1',

            0x02, 0x00,

            0x02, 0x00,
            (byte)'I', (byte)'1',

            0x02, 0x00,
            (byte)'I', (byte)'2'
        };

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Response,
            ProtocolMessageType.DiscoverResponse,
            new CorrelationId(7),
            payload);

        DiscoverResponse response =
            Assert.IsType<DiscoverResponse>(
                codec.Decode(envelope));

        Assert.Equal(
            new EndpointId("EP1"),
            response.EndpointId);

        Assert.Collection(
            response.InstrumentIds,
            id => Assert.Equal(
                new InstrumentId("I1"),
                id),
            id => Assert.Equal(
                new InstrumentId("I2"),
                id));
    }

    [Fact]
    public void DiscoverResponse_RoundTrip_PreservesValues()
    {
        BinaryProtocolPayloadCodec codec = new();

        DiscoverResponse original = new(
            new CorrelationId(9),
            new EndpointId("Endpoint"),
            new[]
            {
                new InstrumentId("A"),
                new InstrumentId("B")
            });

        ProtocolEnvelope envelope =
            codec.Encode(original);

        DiscoverResponse decoded =
            Assert.IsType<DiscoverResponse>(
                codec.Decode(envelope));

        Assert.Equal(
            original.Version,
            decoded.Version);

        Assert.Equal(
            original.Role,
            decoded.Role);

        Assert.Equal(
            original.MessageType,
            decoded.MessageType);

        Assert.Equal(
            original.CorrelationId,
            decoded.CorrelationId);

        Assert.Equal(
            original.EndpointId,
            decoded.EndpointId);

        Assert.Equal(
            original.InstrumentIds,
            decoded.InstrumentIds);
    }

    [Fact]
    public void Decode_DiscoverResponse_WithTrailingBytes_Throws()
    {
        BinaryProtocolPayloadCodec codec = new();

        byte[] payload =
        {
            0x03, 0x00,
            (byte)'E', (byte)'P', (byte)'1',

            0x00, 0x00,

            0xFF
        };

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Response,
            ProtocolMessageType.DiscoverResponse,
            CorrelationId.None,
            payload);

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void Encode_ReadEndpointDescriptorRequest_WritesExpectedPayload()
    {
        BinaryProtocolPayloadCodec codec = new();

        ReadEndpointDescriptorRequest request = new(
            new CorrelationId(17),
            new EndpointId("EP1"));

        ProtocolEnvelope envelope =
            codec.Encode(request);

        Assert.Equal(
            ProtocolVersion.Current,
            envelope.Version);

        Assert.Equal(
            ProtocolMessageRole.Request,
            envelope.Role);

        Assert.Equal(
            ProtocolMessageType.ReadEndpointDescriptorRequest,
            envelope.MessageType);

        Assert.Equal(
            new CorrelationId(17),
            envelope.CorrelationId);

        Assert.Equal(
            new byte[]
            {
                0x03, 0x00,
                (byte)'E', (byte)'P', (byte)'1'
            },
            envelope.Payload.ToArray());
    }

    [Fact]
    public void Decode_ReadEndpointDescriptorRequest_ReadsExpectedMessage()
    {
        BinaryProtocolPayloadCodec codec = new();

        byte[] payload =
        {
            0x03, 0x00,
            (byte)'E', (byte)'P', (byte)'1'
        };

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.ReadEndpointDescriptorRequest,
            new CorrelationId(17),
            payload);

        ReadEndpointDescriptorRequest request =
            Assert.IsType<ReadEndpointDescriptorRequest>(
                codec.Decode(envelope));

        Assert.Equal(
            new CorrelationId(17),
            request.CorrelationId);

        Assert.Equal(
            new EndpointId("EP1"),
            request.EndpointId);
    }

    [Fact]
    public void ReadEndpointDescriptorRequest_RoundTrip_PreservesValues()
    {
        BinaryProtocolPayloadCodec codec = new();

        ReadEndpointDescriptorRequest original = new(
            new CorrelationId(23),
            new EndpointId("Endpoint-1"));

        ProtocolEnvelope envelope =
            codec.Encode(original);

        ReadEndpointDescriptorRequest decoded =
            Assert.IsType<ReadEndpointDescriptorRequest>(
                codec.Decode(envelope));

        Assert.Equal(
            original,
            decoded);
    }

    [Fact]
    public void Decode_ReadEndpointDescriptorRequest_WithWrongRole_Throws()
    {
        BinaryProtocolPayloadCodec codec = new();

        byte[] payload =
        {
            0x03, 0x00,
            (byte)'E', (byte)'P', (byte)'1'
        };

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Response,
            ProtocolMessageType.ReadEndpointDescriptorRequest,
            new CorrelationId(17),
            payload);

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void Decode_ReadEndpointDescriptorRequest_WithTruncatedPayload_Throws()
    {
        BinaryProtocolPayloadCodec codec = new();

        byte[] payload =
        {
            0x03, 0x00,
            (byte)'E', (byte)'P'
        };

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.ReadEndpointDescriptorRequest,
            new CorrelationId(17),
            payload);

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void Decode_ReadEndpointDescriptorRequest_WithTrailingBytes_Throws()
    {
        BinaryProtocolPayloadCodec codec = new();

        byte[] payload =
        {
            0x03, 0x00,
            (byte)'E', (byte)'P', (byte)'1',
            0xFF
        };

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.ReadEndpointDescriptorRequest,
            new CorrelationId(17),
            payload);

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void Encode_UnsupportedMessage_ThrowsNotSupportedException()
    {
        BinaryProtocolPayloadCodec codec = new();

        UnsupportedTestMessage message =
            new(CorrelationId.None);

        Assert.Throws<NotSupportedException>(
            () => codec.Encode(message));
    }

    [Fact]
    public void Encode_ReadEndpointDescriptorResponseWithDescriptor_WritesExpectedHeader()
    {
        BinaryProtocolPayloadCodec codec = new();

        EndpointDescriptor descriptor =
            new(new EndpointId("endpoint"));

        ReadEndpointDescriptorResponse response = new(
            new CorrelationId(31),
            ProtocolResult.Success,
            descriptor);

        ProtocolEnvelope envelope =
            codec.Encode(response);

        Assert.Equal(
            ProtocolVersion.Current,
            envelope.Version);

        Assert.Equal(
            ProtocolMessageRole.Response,
            envelope.Role);

        Assert.Equal(
            ProtocolMessageType.ReadEndpointDescriptorResponse,
            envelope.MessageType);

        Assert.Equal(
            new CorrelationId(31),
            envelope.CorrelationId);

        Assert.False(
            envelope.Payload.IsEmpty);
    }

    [Fact]
    public void ReadEndpointDescriptorResponse_RoundTripWithDescriptor_PreservesValues()
    {
        BinaryProtocolPayloadCodec codec = new();

        EndpointDescriptor descriptor =
            new(new EndpointId("endpoint"))
            {
                Metadata = new EndpointMetadata
                {
                    DisplayName = "Laboratory Endpoint",
                    Description = "Main laboratory endpoint."
                }
            };

        ReadEndpointDescriptorResponse original = new(
            new CorrelationId(32),
            ProtocolResult.Success,
            descriptor);

        ProtocolEnvelope envelope =
            codec.Encode(original);

        ReadEndpointDescriptorResponse decoded =
            Assert.IsType<ReadEndpointDescriptorResponse>(
                codec.Decode(envelope));

        Assert.Equal(
            original.CorrelationId,
            decoded.CorrelationId);

        Assert.Equal(
            original.Result,
            decoded.Result);

        Assert.NotNull(
            decoded.Descriptor);

        Assert.Equal(
            descriptor.Id,
            decoded.Descriptor.Id);

        Assert.Equal(
            descriptor.Metadata,
            decoded.Descriptor.Metadata);

        Assert.Empty(
            decoded.Descriptor.Instruments);
    }

    [Fact]
    public void ReadEndpointDescriptorResponse_RoundTripWithoutDescriptor_PreservesFailure()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolResult result = new(
            ProtocolResultCode.NotFound,
            "Endpoint was not found.");

        ReadEndpointDescriptorResponse original = new(
            new CorrelationId(33),
            result,
            null);

        ProtocolEnvelope envelope =
            codec.Encode(original);

        ReadEndpointDescriptorResponse decoded =
            Assert.IsType<ReadEndpointDescriptorResponse>(
                codec.Decode(envelope));

        Assert.Equal(
            original.CorrelationId,
            decoded.CorrelationId);

        Assert.Equal(
            result,
            decoded.Result);

        Assert.Null(
            decoded.Descriptor);
    }

    [Fact]
    public void Decode_ReadEndpointDescriptorResponseWithWrongRole_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.ReadEndpointDescriptorResponse,
            new CorrelationId(34),
            new byte[]
            {
            (byte)ProtocolResultCode.NotFound,
            0x00,
            0x00
            });

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void Decode_ReadEndpointDescriptorResponseWithUnknownResultCode_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Response,
            ProtocolMessageType.ReadEndpointDescriptorResponse,
            new CorrelationId(35),
            new byte[]
            {
            0xFF,
            0x00,
            0x00
            });

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void Decode_ReadEndpointDescriptorResponseWithInvalidDescriptorMarker_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Response,
            ProtocolMessageType.ReadEndpointDescriptorResponse,
            new CorrelationId(36),
            new byte[]
            {
            (byte)ProtocolResultCode.Success,
            0x00,
            0x02
            });

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void Encode_ReadPropertyRequest_WritesExpectedPayload()
    {
        BinaryProtocolPayloadCodec codec = new();

        ReadPropertyRequest request = new(
            new CorrelationId(41),
            new InstrumentId("sensor"),
            new PropertyId("temperature"));

        ProtocolEnvelope envelope =
            codec.Encode(request);

        Assert.Equal(
            ProtocolMessageRole.Request,
            envelope.Role);

        Assert.Equal(
            ProtocolMessageType.ReadPropertyRequest,
            envelope.MessageType);

        Assert.Equal(
            new CorrelationId(41),
            envelope.CorrelationId);

        Assert.Equal(
            new byte[]
            {
            0x06, 0x00,
            (byte)'s', (byte)'e', (byte)'n',
            (byte)'s', (byte)'o', (byte)'r',

            0x0B, 0x00,
            (byte)'t', (byte)'e', (byte)'m',
            (byte)'p', (byte)'e', (byte)'r',
            (byte)'a', (byte)'t', (byte)'u',
            (byte)'r', (byte)'e'
            },
            envelope.Payload.ToArray());
    }

    [Fact]
    public void ReadPropertyRequest_RoundTrip_PreservesValues()
    {
        BinaryProtocolPayloadCodec codec = new();

        ReadPropertyRequest original = new(
            new CorrelationId(42),
            new InstrumentId("sensor"),
            new PropertyId("temperature"));

        ProtocolEnvelope envelope =
            codec.Encode(original);

        ReadPropertyRequest decoded =
            Assert.IsType<ReadPropertyRequest>(
                codec.Decode(envelope));

        Assert.Equal(
            original,
            decoded);
    }

    [Fact]
    public void Decode_ReadPropertyRequestWithWrongRole_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();

        BinaryProtocolWriter writer = new();

        writer.WriteString("sensor");
        writer.WriteString("temperature");

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Response,
            ProtocolMessageType.ReadPropertyRequest,
            new CorrelationId(43),
            writer.ToArray());

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void Decode_ReadPropertyRequestWithTruncatedPayload_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();

        BinaryProtocolWriter writer = new();

        writer.WriteString("sensor");

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.ReadPropertyRequest,
            new CorrelationId(44),
            writer.ToArray());

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void ReadPropertyResponse_RoundTripWithValue_PreservesValues()
    {
        BinaryProtocolPayloadCodec codec = new();

        PropertyValue propertyValue = new(
            23.5,
            new DateTimeOffset(
                2026,
                7,
                12,
                12,
                30,
                0,
                TimeSpan.Zero),
            PropertyQuality.Good);

        ReadPropertyResponse original = new(
            new CorrelationId(45),
            ProtocolResult.Success,
            propertyValue);

        ProtocolEnvelope envelope =
            codec.Encode(original);

        ReadPropertyResponse decoded =
            Assert.IsType<ReadPropertyResponse>(
                codec.Decode(envelope));

        Assert.Equal(
            original.CorrelationId,
            decoded.CorrelationId);

        Assert.Equal(
            original.Result,
            decoded.Result);

        Assert.Equal(
            propertyValue,
            decoded.PropertyValue);
    }

    [Fact]
    public void ReadPropertyResponse_RoundTripWithoutValue_PreservesFailure()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolResult result = new(
            ProtocolResultCode.NotFound,
            "Property was not found.");

        ReadPropertyResponse original = new(
            new CorrelationId(46),
            result,
            null);

        ProtocolEnvelope envelope =
            codec.Encode(original);

        ReadPropertyResponse decoded =
            Assert.IsType<ReadPropertyResponse>(
                codec.Decode(envelope));

        Assert.Equal(
            original.CorrelationId,
            decoded.CorrelationId);

        Assert.Equal(
            result,
            decoded.Result);

        Assert.Null(
            decoded.PropertyValue);
    }

    [Fact]
    public void Decode_ReadPropertyResponseWithWrongRole_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.ReadPropertyResponse,
            new CorrelationId(47),
            new byte[]
            {
            (byte)ProtocolResultCode.NotFound,
            0x00,
            0x00
            });

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void Decode_ReadPropertyResponseWithInvalidValueMarker_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Response,
            ProtocolMessageType.ReadPropertyResponse,
            new CorrelationId(48),
            new byte[]
            {
            (byte)ProtocolResultCode.Success,
            0x00,
            0x02
            });

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void WritePropertyRequest_RoundTrip_PreservesValues()
    {
        BinaryProtocolPayloadCodec codec = new();

        WritePropertyRequest original = new(
            new CorrelationId(51),
            new InstrumentId("dds"),
            new PropertyId("frequency"),
            145000000);

        ProtocolEnvelope envelope =
            codec.Encode(original);

        WritePropertyRequest decoded =
            Assert.IsType<WritePropertyRequest>(
                codec.Decode(envelope));

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void WritePropertyRequest_RoundTrip_StringValue()
    {
        BinaryProtocolPayloadCodec codec = new();

        WritePropertyRequest original = new(
            new CorrelationId(52),
            new InstrumentId("sensor"),
            new PropertyId("name"),
            "Outdoor");

        ProtocolEnvelope envelope =
            codec.Encode(original);

        WritePropertyRequest decoded =
            Assert.IsType<WritePropertyRequest>(
                codec.Decode(envelope));

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void WritePropertyResponse_RoundTrip_WithPropertyValue()
    {
        BinaryProtocolPayloadCodec codec = new();

        DateTimeOffset timestamp = new(
            2026,
            7,
            12,
            12,
            30,
            0,
            123,
            TimeSpan.Zero);

        PropertyValue propertyValue = new(
            12.5,
            timestamp,
            PropertyQuality.Good);

        WritePropertyResponse original = new(
            new CorrelationId(53),
            ProtocolResult.Success,
            propertyValue);

        ProtocolEnvelope envelope =
            codec.Encode(original);

        WritePropertyResponse decoded =
            Assert.IsType<WritePropertyResponse>(
                codec.Decode(envelope));

        Assert.Equal(
            original.Result,
            decoded.Result);

        Assert.Equal(
            original.PropertyValue,
            decoded.PropertyValue);
    }

    [Fact]
    public void WritePropertyResponse_RoundTrip_WithFailure()
    {
        BinaryProtocolPayloadCodec codec = new();

        WritePropertyResponse original = new(
            new CorrelationId(54),
            new ProtocolResult(
                ProtocolResultCode.Rejected,
                "Read only property."),
            null);

        ProtocolEnvelope envelope =
            codec.Encode(original);

        WritePropertyResponse decoded =
            Assert.IsType<WritePropertyResponse>(
                codec.Decode(envelope));

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Decode_WritePropertyResponse_InvalidMarker_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Response,
            ProtocolMessageType.WritePropertyResponse,
            new CorrelationId(55),
            new byte[]
            {
            (byte)ProtocolResultCode.Success,
            0x00,
            0x02
            });

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void ExecuteCommandRequest_RoundTripWithArgument_PreservesValues()
    {
        BinaryProtocolPayloadCodec codec = new();

        ExecuteCommandRequest original = new(
            new CorrelationId(61),
            new InstrumentId("dds"),
            DescriptorPath.Parse("DDS.Sweep.Start"),
            145000000);

        ProtocolEnvelope envelope =
            codec.Encode(original);

        ExecuteCommandRequest decoded =
            Assert.IsType<ExecuteCommandRequest>(
                codec.Decode(envelope));

        Assert.Equal(
            original,
            decoded);
    }

    [Fact]
    public void ExecuteCommandRequest_RoundTripWithoutArgument_PreservesValues()
    {
        BinaryProtocolPayloadCodec codec = new();

        ExecuteCommandRequest original = new(
            new CorrelationId(62),
            new InstrumentId("dds"),
            DescriptorPath.Parse("DDS.Reset"),
            null);

        ProtocolEnvelope envelope =
            codec.Encode(original);

        ExecuteCommandRequest decoded =
            Assert.IsType<ExecuteCommandRequest>(
                codec.Decode(envelope));

        Assert.Equal(
            original,
            decoded);
    }

    [Fact]
    public void Decode_ExecuteCommandRequestWithWrongRole_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();
        BinaryProtocolWriter writer = new();

        writer.WriteString("dds");
        writer.WriteString("DDS.Reset");

        new VariantSerializer().Write(
            writer,
            null);

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Response,
            ProtocolMessageType.ExecuteCommandRequest,
            new CorrelationId(63),
            writer.ToArray());

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void ExecuteCommandResponse_RoundTripWithReturnValue_PreservesValues()
    {
        BinaryProtocolPayloadCodec codec = new();

        ExecuteCommandResponse original = new(
            new CorrelationId(64),
            ProtocolResult.Success,
            "Completed");

        ProtocolEnvelope envelope =
            codec.Encode(original);

        ExecuteCommandResponse decoded =
            Assert.IsType<ExecuteCommandResponse>(
                codec.Decode(envelope));

        Assert.Equal(
            original,
            decoded);
    }

    [Fact]
    public void ExecuteCommandResponse_RoundTripWithFailure_PreservesValues()
    {
        BinaryProtocolPayloadCodec codec = new();

        ExecuteCommandResponse original = new(
            new CorrelationId(65),
            new ProtocolResult(
                ProtocolResultCode.Rejected,
                "Command was rejected."),
            null);

        ProtocolEnvelope envelope =
            codec.Encode(original);

        ExecuteCommandResponse decoded =
            Assert.IsType<ExecuteCommandResponse>(
                codec.Decode(envelope));

        Assert.Equal(
            original,
            decoded);
    }

    [Fact]
    public void Decode_ExecuteCommandResponseWithWrongRole_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();
        BinaryProtocolWriter writer = new();

        writer.WriteByte(
            (byte)ProtocolResultCode.Success);

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            null);

        new VariantSerializer().Write(
            writer,
            null);

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.ExecuteCommandResponse,
            new CorrelationId(66),
            writer.ToArray());

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void EventNotification_RoundTrip_PreservesValues()
    {
        BinaryProtocolPayloadCodec codec = new();

        DateTimeOffset timestamp = new(
            2026,
            7,
            12,
            15,
            30,
            0,
            250,
            TimeSpan.Zero);

        EventNotification original = new(
            new InstrumentId("environment"),
            DescriptorPath.Parse("Environment.Alarm"),
            timestamp,
            "High temperature");

        ProtocolEnvelope envelope =
            codec.Encode(original);

        EventNotification decoded =
            Assert.IsType<EventNotification>(
                codec.Decode(envelope));

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void EventNotification_RoundTrip_WithNullValue()
    {
        BinaryProtocolPayloadCodec codec = new();

        EventNotification original = new(
            new InstrumentId("environment"),
            DescriptorPath.Parse("Environment.Reset"),
            DateTimeOffset.UnixEpoch,
            null);

        ProtocolEnvelope envelope =
            codec.Encode(original);

        EventNotification decoded =
            Assert.IsType<EventNotification>(
                codec.Decode(envelope));

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Decode_EventNotificationWithWrongRole_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();

        BinaryProtocolWriter writer = new();

        writer.WriteString("environment");
        writer.WriteString("Environment.Alarm");

        WriteUnixMilliseconds(writer, 0);

        new VariantSerializer().Write(
            writer,
            null);

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Response,
            ProtocolMessageType.EventNotification,
            CorrelationId.None,
            writer.ToArray());

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    private static void WriteUnixMilliseconds(
        BinaryProtocolWriter writer,
        long value)
    {
        Span<byte> bytes = stackalloc byte[8];

        System.Buffers.Binary.BinaryPrimitives
            .WriteInt64LittleEndian(bytes, value);

        foreach (byte b in bytes)
        {
            writer.WriteByte(b);
        }
    }

}