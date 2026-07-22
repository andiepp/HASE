namespace Hase.CompactProtocol.Tests;

public sealed class CompactEventNotificationCodecTests
{
    [Fact]
    public void Encode_NoValue_ShouldPreserveWireContract()
    {
        CompactSerialFrame frame =
            CompactEventNotificationCodec.Encode(
                new CompactEventNotification(
                    eventId: 0x01,
                    ReadOnlyMemory<byte>.Empty));

        Assert.Equal(
            (byte)CompactSerialMessageType.EventNotification,
            frame.MessageType);

        Assert.Equal(
            0x00,
            frame.CorrelationId);

        Assert.Equal(
            new byte[]
            {
                0x01
            },
            frame.Payload.ToArray());
    }

    [Fact]
    public void Encode_WithValue_ShouldPreserveOpaqueValue()
    {
        CompactSerialFrame frame =
            CompactEventNotificationCodec.Encode(
                new CompactEventNotification(
                    eventId: 0x02,
                    value:
                    new byte[]
                    {
                        0x10,
                        0x20
                    }));

        Assert.Equal(
            new byte[]
            {
                0x02,
                0x10,
                0x20
            },
            frame.Payload.ToArray());
    }

    [Fact]
    public void Decode_NoValue_ShouldRestoreNotification()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.EventNotification,
                correlationId: 0x00,
                payload:
                [
                    0x01
                ]);

        CompactEventNotification notification =
            CompactEventNotificationCodec.Decode(
                frame);

        Assert.Equal(
            0x01,
            notification.EventId);

        Assert.True(
            notification.Value.IsEmpty);
    }

    [Fact]
    public void Decode_WithValue_ShouldRestoreOpaqueValue()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.EventNotification,
                correlationId: 0x00,
                payload:
                [
                    0x02,
                    0x10,
                    0x20
                ]);

        CompactEventNotification notification =
            CompactEventNotificationCodec.Decode(
                frame);

        Assert.Equal(
            0x02,
            notification.EventId);

        Assert.Equal(
            new byte[]
            {
                0x10,
                0x20
            },
            notification.Value.ToArray());
    }

    [Fact]
    public void Encode_NullNotification_ShouldThrow()
    {
        void Act()
        {
            _ = CompactEventNotificationCodec.Encode(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Decode_NullFrame_ShouldThrow()
    {
        void Act()
        {
            _ = CompactEventNotificationCodec.Decode(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Decode_WrongMessageType_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.ReadPropertyResponse,
                correlationId: 0x00,
                payload:
                [
                    0x01
                ]);

        void Act()
        {
            _ = CompactEventNotificationCodec.Decode(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_NonzeroCorrelation_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.EventNotification,
                correlationId: 0x21,
                payload:
                [
                    0x01
                ]);

        void Act()
        {
            _ = CompactEventNotificationCodec.Decode(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_EmptyPayload_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.EventNotification,
                correlationId: 0x00,
                payload: []);

        void Act()
        {
            _ = CompactEventNotificationCodec.Decode(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_ZeroEventId_ShouldThrow()
    {
        var frame =
            new CompactSerialFrame(
                (byte)CompactSerialMessageType.EventNotification,
                correlationId: 0x00,
                payload:
                [
                    0x00
                ]);

        void Act()
        {
            _ = CompactEventNotificationCodec.Decode(
                frame);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Notification_ZeroEventId_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEventNotification(
                eventId: 0x00,
                ReadOnlyMemory<byte>.Empty);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Notification_Value_ShouldBeCopied()
    {
        byte[] value =
        [
            0x10,
            0x20
        ];

        var notification =
            new CompactEventNotification(
                eventId: 0x01,
                value);

        value[0] =
            0xFF;

        Assert.Equal(
            new byte[]
            {
                0x10,
                0x20
            },
            notification.Value.ToArray());
    }

    [Fact]
    public void Encode_NoValueFrame_ShouldMatchGoldenBytes()
    {
        CompactSerialFrame frame =
            CompactEventNotificationCodec.Encode(
                new CompactEventNotification(
                    eventId: 0x01,
                    ReadOnlyMemory<byte>.Empty));

        byte[] encoded =
            CompactSerialFrameCodec.Encode(
                frame);

        Assert.Equal(
            new byte[]
            {
                0x48,
                0x53,
                0x01,
                0x09,
                0x00,
                0x01,
                0x01,
                0x6B,
                0x3A
            },
            encoded);
    }

    [Fact]
    public void Decode_GoldenBytes_ShouldRestoreNoValueEvent()
    {
        byte[] encoded =
        [
            0x48,
            0x53,
            0x01,
            0x09,
            0x00,
            0x01,
            0x01,
            0x6B,
            0x3A
        ];

        CompactSerialFrame frame =
            CompactSerialFrameCodec.Decode(
                encoded);

        CompactEventNotification notification =
            CompactEventNotificationCodec.Decode(
                frame);

        Assert.Equal(
            0x01,
            notification.EventId);

        Assert.True(
            notification.Value.IsEmpty);
    }
}