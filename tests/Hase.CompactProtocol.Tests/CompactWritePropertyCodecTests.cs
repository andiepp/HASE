namespace Hase.CompactProtocol.Tests;

public sealed class CompactWritePropertyCodecTests
{
    [Fact]
    public void EncodeRequest_ShouldPreserveWireContract()
    {
        CompactSerialFrame frame =
            CompactWritePropertyCodec.EncodeRequest(
                new CompactWritePropertyRequest(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    value: new byte[]
                    {
                        0x01
                    }));

        Assert.Equal(
            (byte)CompactSerialMessageType.WritePropertyRequest,
            frame.MessageType);

        Assert.Equal(
            0x2A,
            frame.CorrelationId);

        Assert.Equal(
            new byte[]
            {
                0x01,
                0x01
            },
            frame.Payload.ToArray());
    }

    [Fact]
    public void DecodeRequest_ValidFrame_ShouldRestoreRequest()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.WritePropertyRequest,
                correlationId: 0x2A,
                payload:
                [
                    0x01,
                    0x01
                ]);

        CompactWritePropertyRequest request =
            CompactWritePropertyCodec.DecodeRequest(
                frame);

        Assert.Equal(
            0x2A,
            request.CorrelationId);

        Assert.Equal(
            0x01,
            request.PropertyId);

        Assert.Equal(
            new byte[]
            {
                0x01
            },
            request.Value.ToArray());
    }

    [Theory]
    [InlineData(
        0x00)]
    [InlineData(
        0x01)]
    [InlineData(
        0x02)]
    [InlineData(
        0x03)]
    [InlineData(
        0x04)]
    public void Response_DefinedStatus_ShouldRoundTrip(
        byte statusByte)
    {
        CompactPropertyWriteStatus status =
            (CompactPropertyWriteStatus)statusByte;

        var response =
            new CompactWritePropertyResponse(
                correlationId: 0x2A,
                propertyId: 0x01,
                status);

        CompactSerialFrame frame =
            CompactWritePropertyCodec.EncodeResponse(
                response);

        Assert.Equal(
            (byte)CompactSerialMessageType.WritePropertyResponse,
            frame.MessageType);

        Assert.Equal(
            new byte[]
            {
                0x01,
                statusByte
            },
            frame.Payload.ToArray());

        CompactWritePropertyResponse decoded =
            CompactWritePropertyCodec.DecodeResponse(
                frame);

        Assert.Equal(
            0x2A,
            decoded.CorrelationId);

        Assert.Equal(
            0x01,
            decoded.PropertyId);

        Assert.Equal(
            status,
            decoded.Status);
    }

    [Fact]
    public void Request_ZeroCorrelation_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactWritePropertyRequest(
                correlationId: 0,
                propertyId: 0x01,
                value: new byte[]
                {
                    0x01
                });
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Request_ZeroPropertyId_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactWritePropertyRequest(
                correlationId: 0x2A,
                propertyId: 0,
                value: new byte[]
                {
                    0x01
                });
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Request_EmptyValue_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactWritePropertyRequest(
                correlationId: 0x2A,
                propertyId: 0x01,
                ReadOnlyMemory<byte>.Empty);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Request_ShouldOwnValueBytes()
    {
        byte[] value =
        [
            0x01
        ];

        var request =
            new CompactWritePropertyRequest(
                correlationId: 0x2A,
                propertyId: 0x01,
                value);

        value[0] =
            0x00;

        Assert.Equal(
            new byte[]
            {
                0x01
            },
            request.Value.ToArray());
    }

    [Fact]
    public void Response_UndefinedStatus_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactWritePropertyResponse(
                correlationId: 0x2A,
                propertyId: 0x01,
                (CompactPropertyWriteStatus)0xFF);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void DecodeRequest_MissingValue_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.WritePropertyRequest,
                correlationId: 0x2A,
                payload:
                [
                    0x01
                ]);

        void Act()
        {
            _ = CompactWritePropertyCodec.DecodeRequest(
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
                (byte)CompactSerialMessageType.WritePropertyRequest,
                correlationId: 0x2A,
                payload:
                [
                    0x00,
                    0x01
                ]);

        void Act()
        {
            _ = CompactWritePropertyCodec.DecodeRequest(
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
                (byte)CompactSerialMessageType.ReadPropertyRequest,
                correlationId: 0x2A,
                payload:
                [
                    0x01,
                    0x01
                ]);

        void Act()
        {
            _ = CompactWritePropertyCodec.DecodeRequest(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeResponse_WrongPayloadLength_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.WritePropertyResponse,
                correlationId: 0x2A,
                payload:
                [
                    0x01
                ]);

        void Act()
        {
            _ = CompactWritePropertyCodec.DecodeResponse(
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
                (byte)CompactSerialMessageType.WritePropertyResponse,
                correlationId: 0x2A,
                payload:
                [
                    0x01,
                    0xFF
                ]);

        void Act()
        {
            _ = CompactWritePropertyCodec.DecodeResponse(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeResponse_ZeroPropertyId_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.WritePropertyResponse,
                correlationId: 0x2A,
                payload:
                [
                    0x00,
                    0x00
                ]);

        void Act()
        {
            _ = CompactWritePropertyCodec.DecodeResponse(
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
                (byte)CompactSerialMessageType.ReadPropertyResponse,
                correlationId: 0x2A,
                payload:
                [
                    0x01,
                    0x00
                ]);

        void Act()
        {
            _ = CompactWritePropertyCodec.DecodeResponse(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }
}