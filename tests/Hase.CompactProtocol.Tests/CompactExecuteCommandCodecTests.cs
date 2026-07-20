namespace Hase.CompactProtocol.Tests;

public sealed class CompactExecuteCommandCodecTests
{
    [Fact]
    public void EncodeRequest_KnownRequest_ShouldMatchGoldenFrame()
    {
        CompactSerialFrame frame =
            CompactExecuteCommandCodec.EncodeRequest(
                new CompactExecuteCommandRequest(
                    correlationId: 0x2A,
                    commandId: 0x01));

        byte[] encoded =
            CompactSerialFrameCodec.Encode(
                frame);

        byte[] expected =
        [
            0x48,
            0x53,
            0x01,
            0x03,
            0x2A,
            0x01,
            0x01,
            0x42,
            0x96
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
                (byte)CompactSerialMessageType.ExecuteCommandRequest,
                correlationId: 0x2A,
                payload:
                [
                    0x01
                ]);

        CompactExecuteCommandRequest request =
            CompactExecuteCommandCodec.DecodeRequest(
                frame);

        Assert.Equal(
            0x2A,
            request.CorrelationId);

        Assert.Equal(
            0x01,
            request.CommandId);
    }

    [Fact]
    public void EncodeResponse_KnownResponse_ShouldMatchGoldenFrame()
    {
        CompactSerialFrame frame =
            CompactExecuteCommandCodec.EncodeResponse(
                new CompactExecuteCommandResponse(
                    correlationId: 0x2A,
                    commandId: 0x01,
                    CompactCommandExecutionStatus.Success));

        byte[] encoded =
            CompactSerialFrameCodec.Encode(
                frame);

        byte[] expected =
        [
            0x48,
            0x53,
            0x01,
            0x04,
            0x2A,
            0x02,
            0x01,
            0x00,
            0xC0,
            0x02
        ];

        Assert.Equal(
            expected,
            encoded);
    }

    [Theory]
    [InlineData(
        0x00)]
    [InlineData(
        0x01)]
    [InlineData(
        0x02)]
    public void Response_KnownStatus_ShouldRoundTrip(
        byte statusByte)
    {
        CompactCommandExecutionStatus status =
            (CompactCommandExecutionStatus)statusByte;

        var response =
            new CompactExecuteCommandResponse(
                correlationId: 0x2A,
                commandId: 0x01,
                status);

        CompactSerialFrame frame =
            CompactExecuteCommandCodec.EncodeResponse(
                response);

        Assert.Equal(
            statusByte,
            frame.Payload.Span[1]);

        CompactExecuteCommandResponse decoded =
            CompactExecuteCommandCodec.DecodeResponse(
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
            _ = new CompactExecuteCommandRequest(
                correlationId: 0,
                commandId: 0x01);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Request_ZeroCommandId_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactExecuteCommandRequest(
                correlationId: 0x2A,
                commandId: 0);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Response_ZeroCorrelation_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactExecuteCommandResponse(
                correlationId: 0,
                commandId: 0x01,
                CompactCommandExecutionStatus.Success);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Response_ZeroCommandId_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactExecuteCommandResponse(
                correlationId: 0x2A,
                commandId: 0,
                CompactCommandExecutionStatus.Success);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Response_UndefinedStatus_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactExecuteCommandResponse(
                correlationId: 0x2A,
                commandId: 0x01,
                (CompactCommandExecutionStatus)0xFF);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void DecodeRequest_WrongPayloadLength_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.ExecuteCommandRequest,
                correlationId: 0x2A,
                payload: []);

        void Act()
        {
            _ = CompactExecuteCommandCodec.DecodeRequest(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeRequest_ZeroCommandId_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.ExecuteCommandRequest,
                correlationId: 0x2A,
                payload:
                [
                    0x00
                ]);

        void Act()
        {
            _ = CompactExecuteCommandCodec.DecodeRequest(
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
            _ = CompactExecuteCommandCodec.DecodeRequest(
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
                (byte)CompactSerialMessageType.ExecuteCommandResponse,
                correlationId: 0x2A,
                payload:
                [
                    0x01
                ]);

        void Act()
        {
            _ = CompactExecuteCommandCodec.DecodeResponse(
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
                (byte)CompactSerialMessageType.ExecuteCommandResponse,
                correlationId: 0x2A,
                payload:
                [
                    0x01,
                    0xFF
                ]);

        void Act()
        {
            _ = CompactExecuteCommandCodec.DecodeResponse(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void DecodeResponse_ZeroCommandId_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.ExecuteCommandResponse,
                correlationId: 0x2A,
                payload:
                [
                    0x00,
                    0x00
                ]);

        void Act()
        {
            _ = CompactExecuteCommandCodec.DecodeResponse(
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
                    0x00
                ]);

        void Act()
        {
            _ = CompactExecuteCommandCodec.DecodeResponse(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }
}