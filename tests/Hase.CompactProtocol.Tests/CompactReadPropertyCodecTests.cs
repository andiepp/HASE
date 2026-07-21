namespace Hase.CompactProtocol.Tests;

public sealed class CompactReadPropertyCodecTests
{
    [Fact]
    public void EncodeRequest_ShouldPreserveWireContract()
    {
        CompactSerialFrame frame =
            CompactReadPropertyCodec.EncodeRequest(
                new CompactReadPropertyRequest(
                    correlationId: 0x2A,
                    propertyId: 0x01));

        Assert.Equal(
            (byte)CompactSerialMessageType.ReadPropertyRequest,
            frame.MessageType);

        Assert.Equal(
            0x2A,
            frame.CorrelationId);

        Assert.Equal(
            new byte[]
            {
                0x01
            },
            frame.Payload.ToArray());
    }

    [Fact]
    public void DecodeRequest_ValidFrame_ShouldRestoreRequest()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.ReadPropertyRequest,
                correlationId: 0x2A,
                payload:
                [
                    0x01
                ]);

        CompactReadPropertyRequest request =
            CompactReadPropertyCodec.DecodeRequest(
                frame);

        Assert.Equal(
            0x2A,
            request.CorrelationId);

        Assert.Equal(
            0x01,
            request.PropertyId);
    }

    [Fact]
    public void EncodeResponse_Success_ShouldPreserveOpaqueValue()
    {
        var response =
            new CompactReadPropertyResponse(
                correlationId: 0x2A,
                propertyId: 0x01,
                CompactPropertyReadStatus.Success,
                value: new byte[]
                {
                    0x00,
                    0x00,
                    0xCC,
                    0x41
                });

        CompactSerialFrame frame =
            CompactReadPropertyCodec.EncodeResponse(
                response);

        Assert.Equal(
            (byte)CompactSerialMessageType.ReadPropertyResponse,
            frame.MessageType);

        Assert.Equal(
            0x2A,
            frame.CorrelationId);

        Assert.Equal(
            new byte[]
            {
                0x01,
                0x00,
                0x00,
                0x00,
                0xCC,
                0x41
            },
            frame.Payload.ToArray());
    }

    [Fact]
    public void DecodeResponse_Success_ShouldRestoreOpaqueValue()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.ReadPropertyResponse,
                correlationId: 0x2A,
                payload:
                [
                    0x01,
                    0x00,
                    0x00,
                    0x00,
                    0xCC,
                    0x41
                ]);

        CompactReadPropertyResponse response =
            CompactReadPropertyCodec.DecodeResponse(
                frame);

        Assert.Equal(
            0x2A,
            response.CorrelationId);

        Assert.Equal(
            0x01,
            response.PropertyId);

        Assert.Equal(
            CompactPropertyReadStatus.Success,
            response.Status);

        Assert.Equal(
            new byte[]
            {
                0x00,
                0x00,
                0xCC,
                0x41
            },
            response.Value.ToArray());
    }

    [Theory]
    [InlineData(
        0x01)]
    [InlineData(
        0x02)]
    public void Response_FailureStatus_ShouldRoundTripWithoutValue(
        byte statusByte)
    {
        CompactPropertyReadStatus status =
            (CompactPropertyReadStatus)statusByte;

        var response =
            new CompactReadPropertyResponse(
                correlationId: 0x2A,
                propertyId: 0x01,
                status,
                ReadOnlyMemory<byte>.Empty);

        CompactSerialFrame frame =
            CompactReadPropertyCodec.EncodeResponse(
                response);

        CompactReadPropertyResponse decoded =
            CompactReadPropertyCodec.DecodeResponse(
                frame);

        Assert.Equal(
            status,
            decoded.Status);

        Assert.True(
            decoded.Value.IsEmpty);
    }

    [Fact]
    public void Request_ZeroCorrelation_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactReadPropertyRequest(
                correlationId: 0,
                propertyId: 0x01);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Request_ZeroPropertyId_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactReadPropertyRequest(
                correlationId: 0x2A,
                propertyId: 0);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Response_SuccessWithoutValue_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactReadPropertyResponse(
                correlationId: 0x2A,
                propertyId: 0x01,
                CompactPropertyReadStatus.Success,
                ReadOnlyMemory<byte>.Empty);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Response_FailureWithValue_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactReadPropertyResponse(
                correlationId: 0x2A,
                propertyId: 0x01,
                CompactPropertyReadStatus.ReadFailed,
                value: new byte[]
                {
                    0x01
                });
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Response_UndefinedStatus_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactReadPropertyResponse(
                correlationId: 0x2A,
                propertyId: 0x01,
                (CompactPropertyReadStatus)0xFF,
                ReadOnlyMemory<byte>.Empty);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void DecodeRequest_WrongPayloadLength_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.ReadPropertyRequest,
                correlationId: 0x2A,
                payload: []);

        void Act()
        {
            _ = CompactReadPropertyCodec.DecodeRequest(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeRequest_ZeroPropertyId_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.ReadPropertyRequest,
                correlationId: 0x2A,
                payload:
                [
                    0x00
                ]);

        void Act()
        {
            _ = CompactReadPropertyCodec.DecodeRequest(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeRequest_WrongMessageType_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.BootstrapRequest,
                correlationId: 0x2A,
                payload:
                [
                    0x01
                ]);

        void Act()
        {
            _ = CompactReadPropertyCodec.DecodeRequest(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeResponse_MissingStatus_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.ReadPropertyResponse,
                correlationId: 0x2A,
                payload:
                [
                    0x01
                ]);

        void Act()
        {
            _ = CompactReadPropertyCodec.DecodeResponse(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeResponse_UnknownStatus_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.ReadPropertyResponse,
                correlationId: 0x2A,
                payload:
                [
                    0x01,
                    0xFF
                ]);

        void Act()
        {
            _ = CompactReadPropertyCodec.DecodeResponse(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeResponse_SuccessWithoutValue_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.ReadPropertyResponse,
                correlationId: 0x2A,
                payload:
                [
                    0x01,
                    0x00
                ]);

        void Act()
        {
            _ = CompactReadPropertyCodec.DecodeResponse(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeResponse_FailureWithValue_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.ReadPropertyResponse,
                correlationId: 0x2A,
                payload:
                [
                    0x01,
                    0x02,
                    0x01
                ]);

        void Act()
        {
            _ = CompactReadPropertyCodec.DecodeResponse(
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
                (byte)CompactSerialMessageType.BootstrapResponse,
                correlationId: 0x2A,
                payload:
                [
                    0x01,
                    0x00,
                    0x01
                ]);

        void Act()
        {
            _ = CompactReadPropertyCodec.DecodeResponse(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }
}