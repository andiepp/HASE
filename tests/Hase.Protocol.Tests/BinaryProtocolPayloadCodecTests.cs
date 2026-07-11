using Hase.Core.Domain.Identity;
using Hase.Protocol;

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
            ProtocolMessageType.DiscoverRequest,
            envelope.MessageType);

        Assert.True(envelope.Payload.IsEmpty);
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
            new byte[]
            {
                0x03,0x00,(byte)'E',(byte)'P',(byte)'1',
                0x02,0x00,
                0x02,0x00,(byte)'I',(byte)'1',
                0x02,0x00,(byte)'I',(byte)'2'
            },
            envelope.Payload.ToArray());
    }

    [Fact]
    public void Decode_DiscoverResponse_ReadsExpectedMessage()
    {
        BinaryProtocolPayloadCodec codec = new();

        byte[] payload =
        {
            0x03,0x00,(byte)'E',(byte)'P',(byte)'1',
            0x02,0x00,
            0x02,0x00,(byte)'I',(byte)'1',
            0x02,0x00,(byte)'I',(byte)'2'
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
            id => Assert.Equal(new InstrumentId("I1"), id),
            id => Assert.Equal(new InstrumentId("I2"), id));
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
            0x03,0x00,(byte)'E',(byte)'P',(byte)'1',
            0x00,0x00,
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
    public void Encode_UnsupportedMessage_ThrowsNotSupportedException()
    {
        BinaryProtocolPayloadCodec codec = new();

        Assert.Throws<NotSupportedException>(
            () => codec.Encode(
                new UnsupportedTestMessage(
                    CorrelationId.None)));
    }
}