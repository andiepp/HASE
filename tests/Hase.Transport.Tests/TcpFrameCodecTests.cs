using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpFrameCodecTests
{
    [Fact]
    public void HeaderLength_ShouldBeFourBytes()
    {
        // Assert
        Assert.Equal(
            4,
            TcpFrameCodec.HeaderLength);
    }

    [Fact]
    public void Encode_ShouldPrefixPayloadWithBigEndianLength()
    {
        // Arrange
        byte[] payload =
        [
            0x10,
            0x20,
            0x30
        ];

        // Act
        byte[] frame =
            TcpFrameCodec.Encode(
                payload);

        // Assert
        Assert.Equal(
            new byte[]
            {
                0x00,
                0x00,
                0x00,
                0x03,
                0x10,
                0x20,
                0x30
            },
            frame);
    }

    [Fact]
    public void Encode_EmptyPayload_ShouldCreateHeaderOnlyFrame()
    {
        // Act
        byte[] frame =
            TcpFrameCodec.Encode(
                Array.Empty<byte>());

        // Assert
        Assert.Equal(
            new byte[]
            {
                0x00,
                0x00,
                0x00,
                0x00
            },
            frame);
    }

    [Fact]
    public void Encode_ShouldNotModifyPayload()
    {
        // Arrange
        byte[] payload =
        [
            0x01,
            0x02,
            0x03
        ];

        byte[] expectedPayload =
        [
            0x01,
            0x02,
            0x03
        ];

        // Act
        _ = TcpFrameCodec.Encode(
            payload);

        // Assert
        Assert.Equal(
            expectedPayload,
            payload);
    }

    [Fact]
    public void Encode_ShouldCopyPayloadIntoFrame()
    {
        // Arrange
        byte[] payload =
        [
            0x01,
            0x02,
            0x03
        ];

        // Act
        byte[] frame =
            TcpFrameCodec.Encode(
                payload);

        payload[0] =
            0xFF;

        // Assert
        Assert.Equal(
            0x01,
            frame[TcpFrameCodec.HeaderLength]);
    }

    [Fact]
    public void Encode_NullPayload_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = TcpFrameCodec.Encode(
                null!);
        }

        // Assert
        Assert.Throws<
            ArgumentNullException>(
                Act);
    }
}