using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactBootstrapCodecTests
{
    [Fact]
    public void EncodeRequest_KnownRequest_ShouldMatchGoldenFrame()
    {
        CompactSerialFrame frame =
            CompactBootstrapCodec.EncodeRequest(
                new CompactBootstrapRequest(
                    correlationId: 0x2A));

        byte[] encoded =
            CompactSerialFrameCodec.Encode(
                frame);

        byte[] expected =
        [
            0x48,
            0x53,
            0x01,
            0x01,
            0x2A,
            0x00,
            0x2C,
            0x69
        ];

        Assert.Equal(
            expected,
            encoded);
    }

    [Fact]
    public void DecodeRequest_ValidFrame_ShouldRestoreRequest()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.BootstrapRequest,
                correlationId: 0x2A,
                payload: []);

        CompactBootstrapRequest request =
            CompactBootstrapCodec.DecodeRequest(
                frame);

        Assert.Equal(
            0x2A,
            request.CorrelationId);
    }

    [Fact]
    public void EncodeResponse_KnownResponse_ShouldMatchGoldenFrame()
    {
        CompactBootstrapResponse response =
            CreateResponse();

        CompactSerialFrame frame =
            CompactBootstrapCodec.EncodeResponse(
                response);

        byte[] encoded =
            CompactSerialFrameCodec.Encode(
                frame);

        byte[] expected =
        [
            0x48,
            0x53,
            0x01,
            0x02,
            0x2A,
            0x11,
            0x06,
            0x75,
            0x6E,
            0x6F,
            0x2D,
            0x30,
            0x31,
            0x07,
            0x65,
            0x6E,
            0x76,
            0x2D,
            0x75,
            0x6E,
            0x6F,
            0x00,
            0x01,
            0xBB,
            0x2B
        ];

        Assert.Equal(
            expected,
            encoded);
    }

    [Fact]
    public void DecodeResponse_GoldenPayload_ShouldRestoreIdentityAndReference()
    {
        byte[] payload =
        [
            0x06,
            0x75,
            0x6E,
            0x6F,
            0x2D,
            0x30,
            0x31,
            0x07,
            0x65,
            0x6E,
            0x76,
            0x2D,
            0x75,
            0x6E,
            0x6F,
            0x00,
            0x01
        ];

        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.BootstrapResponse,
                correlationId: 0x2A,
                payload);

        CompactBootstrapResponse response =
            CompactBootstrapCodec.DecodeResponse(
                frame);

        Assert.Equal(
            "uno-01",
            response.EndpointId.Value);

        Assert.Equal(
            "env-uno",
            response.DescriptorReference.Id.Value);

        Assert.Equal(
            1,
            response.DescriptorReference.Version);
    }

    [Fact]
    public void Response_NonAsciiIdentifiers_ShouldRoundTripUtf8()
    {
        var response =
            new CompactBootstrapResponse(
                correlationId: 0x2A,
                new EndpointId(
                    "gerät-01"),
                new DescriptorReference(
                    new DescriptorId(
                        "fühler"),
                    version: 2));

        CompactSerialFrame frame =
            CompactBootstrapCodec.EncodeResponse(
                response);

        CompactBootstrapResponse decoded =
            CompactBootstrapCodec.DecodeResponse(
                frame);

        Assert.Equal(
            response,
            decoded);
    }

    [Fact]
    public void Request_ZeroCorrelation_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactBootstrapRequest(
                correlationId: 0);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Response_ZeroCorrelation_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactBootstrapResponse(
                correlationId: 0,
                new EndpointId(
                    "uno-01"),
                new DescriptorReference(
                    new DescriptorId(
                        "env-uno"),
                    version: 1));
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void EncodeResponse_IdentifierOver63Utf8Bytes_ShouldThrow()
    {
        var response =
            new CompactBootstrapResponse(
                correlationId: 0x2A,
                new EndpointId(
                    new string(
                        'a',
                        CompactBootstrapCodec.MaximumIdentifierByteLength
                        + 1)),
                new DescriptorReference(
                    new DescriptorId(
                        "env-uno"),
                    version: 1));

        void Act()
        {
            _ = CompactBootstrapCodec.EncodeResponse(
                response);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void DecodeRequest_NonEmptyPayload_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.BootstrapRequest,
                correlationId: 0x2A,
                payload:
                [
                    0x00
                ]);

        void Act()
        {
            _ = CompactBootstrapCodec.DecodeRequest(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeResponse_ZeroDescriptorVersion_ShouldThrow()
    {
        byte[] payload =
        [
            0x01,
            0x61,
            0x01,
            0x62,
            0x00,
            0x00
        ];

        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.BootstrapResponse,
                correlationId: 0x2A,
                payload);

        void Act()
        {
            _ = CompactBootstrapCodec.DecodeResponse(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeResponse_TruncatedIdentifier_ShouldThrow()
    {
        byte[] payload =
        [
            0x02,
            0x61
        ];

        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.BootstrapResponse,
                correlationId: 0x2A,
                payload);

        void Act()
        {
            _ = CompactBootstrapCodec.DecodeResponse(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeResponse_InvalidUtf8_ShouldThrow()
    {
        byte[] payload =
        [
            0x01,
            0xFF,
            0x01,
            0x62,
            0x00,
            0x01
        ];

        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.BootstrapResponse,
                correlationId: 0x2A,
                payload);

        void Act()
        {
            _ = CompactBootstrapCodec.DecodeResponse(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeResponse_WrongMessageType_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.BootstrapRequest,
                correlationId: 0x2A,
                payload: []);

        void Act()
        {
            _ = CompactBootstrapCodec.DecodeResponse(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    private static CompactBootstrapResponse CreateResponse()
    {
        return new CompactBootstrapResponse(
            correlationId: 0x2A,
            new EndpointId(
                "uno-01"),
            new DescriptorReference(
                new DescriptorId(
                    "env-uno"),
                version: 1));
    }
}