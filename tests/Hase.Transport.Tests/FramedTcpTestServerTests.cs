using System.Net;
using System.Net.Sockets;
using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class FramedTcpTestServerTests
{
    [Fact]
    public async Task ServeSingleExchangeAsync_ShouldReceiveAndReturnFramedPayload()
    {
        // Arrange
        byte[] expectedRequest =
        [
            0x01,
            0x02,
            0x03
        ];

        byte[] expectedResponse =
        [
            0x10,
            0x20
        ];

        byte[]? receivedRequest =
            null;

        await using var server =
            new FramedTcpTestServer();

        Task serverTask =
            server.ServeSingleExchangeAsync(
                (
                    request,
                    cancellationToken) =>
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    receivedRequest =
                        request;

                    return Task.FromResult(
                        expectedResponse);
                });

        using var client =
            new TcpClient();

        await client.ConnectAsync(
            IPAddress.Loopback,
            server.Port);

        await using NetworkStream stream =
            client.GetStream();

        byte[] requestFrame =
            TcpFrameCodec.Encode(
                expectedRequest);

        // Act
        await stream.WriteAsync(
            requestFrame.AsMemory());

        await stream.FlushAsync();

        byte[] actualResponse =
            await TcpFrameReader.ReadAsync(
                stream,
                maximumPayloadLength: 1024);

        await serverTask;

        // Assert
        Assert.Equal(
            expectedRequest,
            receivedRequest);

        Assert.Equal(
            expectedResponse,
            actualResponse);
    }

    [Fact]
    public async Task ServeSingleExchangeAsync_FragmentedRequest_ShouldReadCompletePayload()
    {
        // Arrange
        byte[] expectedRequest =
        [
            0x11,
            0x22,
            0x33,
            0x44
        ];

        byte[]? receivedRequest =
            null;

        await using var server =
            new FramedTcpTestServer();

        Task serverTask =
            server.ServeSingleExchangeAsync(
                (
                    request,
                    cancellationToken) =>
                {
                    receivedRequest =
                        request;

                    return Task.FromResult(
                        Array.Empty<byte>());
                });

        using var client =
            new TcpClient();

        await client.ConnectAsync(
            IPAddress.Loopback,
            server.Port);

        await using NetworkStream stream =
            client.GetStream();

        byte[] requestFrame =
            TcpFrameCodec.Encode(
                expectedRequest);

        // Act
        foreach (byte value in requestFrame)
        {
            await stream.WriteAsync(
                new byte[]
                {
                    value
                });

            await stream.FlushAsync();
        }

        byte[] response =
            await TcpFrameReader.ReadAsync(
                stream,
                maximumPayloadLength: 1024);

        await serverTask;

        // Assert
        Assert.Equal(
            expectedRequest,
            receivedRequest);

        Assert.Empty(
            response);
    }

    [Fact]
    public async Task Constructor_NegativeMaximumPayloadLength_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new FramedTcpTestServer(
                maximumPayloadLength: -1);
        }

        // Assert
        ArgumentOutOfRangeException exception =
            Assert.Throws<
                ArgumentOutOfRangeException>(
                    Act);

        Assert.Equal(
            "maximumPayloadLength",
            exception.ParamName);

        await Task.CompletedTask;
    }
}