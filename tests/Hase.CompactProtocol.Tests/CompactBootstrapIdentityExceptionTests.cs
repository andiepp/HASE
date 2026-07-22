namespace Hase.CompactProtocol.Tests;

public sealed class CompactBootstrapIdentityExceptionTests
{
    [Fact]
    public void Constructor_ShouldPreserveInnerException()
    {
        var innerException =
            new ArgumentException(
                "Invalid identity.");

        var exception =
            new CompactBootstrapIdentityException(
                innerException);

        Assert.Same(
            innerException,
            exception.InnerException);

        Assert.Contains(
            "invalid identity",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Exception_ShouldBeAnIOException()
    {
        var exception =
            new CompactBootstrapIdentityException(
                new ArgumentException(
                    "Invalid identity."));

        Assert.IsAssignableFrom<IOException>(
            exception);
    }

    [Fact]
    public void DecodeResponse_WhitespaceEndpointIdentity_ShouldThrowSemanticException()
    {
        byte[] payload =
        [
            0x01,
            0x20,
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

        void Act()
        {
            _ = CompactBootstrapCodec.DecodeResponse(
                frame);
        }

        CompactBootstrapIdentityException exception =
            Assert.Throws<CompactBootstrapIdentityException>(
                Act);

        Assert.IsType<ArgumentException>(
            exception.InnerException);
    }

    [Fact]
    public void DecodeResponse_WhitespaceDescriptorIdentity_ShouldThrowSemanticException()
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
            0x01,
            0x20,
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

        CompactBootstrapIdentityException exception =
            Assert.Throws<CompactBootstrapIdentityException>(
                Act);

        Assert.IsType<ArgumentException>(
            exception.InnerException);
    }
}