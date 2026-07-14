using System.Net;
using System.Net.Sockets;
using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpTransportConnectionConcurrencyTests
{
    [Fact]
    public async Task ExchangeAsync_ConcurrentCalls_ShouldBeSerialized()
    {
        // Arrange
        byte[] firstRequest =
        [
            0x01
        ];

        byte[] firstResponse =
        [
            0x11
        ];

        byte[] secondRequest =
        [
            0x02
        ];

        byte[] secondResponse =
        [
            0x22
        ];

        using var listener =
            new TcpListener(
                IPAddress.Loopback,
                0);

        listener.Start();

        int port =
            ((IPEndPoint)listener.LocalEndpoint)
            .Port;

        Task<TcpClient> acceptTask =
            listener.AcceptTcpClientAsync();

        using var client =
            new TcpClient();

        await client.ConnectAsync(
            IPAddress.Loopback,
            port);

        using TcpClient serverClient =
            await acceptTask;

        await using NetworkStream serverStream =
            serverClient.GetStream();

        await using var connection =
            new TcpTransportConnection(
                client,
                maximumPayloadLength: 1024);

        Task<byte[]> firstExchangeTask =
            connection.ExchangeAsync(
                firstRequest);

        byte[] actualFirstRequest =
            await TcpFrameReader.ReadAsync(
                serverStream,
                maximumPayloadLength: 1024);

        Task<byte[]> secondExchangeTask =
            connection.ExchangeAsync(
                secondRequest);

        using var prematureSecondRequestCancellation =
            new CancellationTokenSource(
                TimeSpan.FromMilliseconds(
                    250));

        Task ReadPrematureSecondRequest()
        {
            return TcpFrameReader.ReadAsync(
                serverStream,
                maximumPayloadLength: 1024,
                prematureSecondRequestCancellation.Token);
        }

        // Act
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                ReadPrematureSecondRequest);

        byte[] firstResponseFrame =
            TcpFrameCodec.Encode(
                firstResponse);

        await serverStream.WriteAsync(
            firstResponseFrame.AsMemory());

        await serverStream.FlushAsync();

        byte[] actualSecondRequest =
            await TcpFrameReader.ReadAsync(
                serverStream,
                maximumPayloadLength: 1024);

        byte[] secondResponseFrame =
            TcpFrameCodec.Encode(
                secondResponse);

        await serverStream.WriteAsync(
            secondResponseFrame.AsMemory());

        await serverStream.FlushAsync();

        byte[] actualFirstResponse =
            await firstExchangeTask;

        byte[] actualSecondResponse =
            await secondExchangeTask;

        // Assert
        Assert.Equal(
            firstRequest,
            actualFirstRequest);

        Assert.Equal(
            secondRequest,
            actualSecondRequest);

        Assert.Equal(
            firstResponse,
            actualFirstResponse);

        Assert.Equal(
            secondResponse,
            actualSecondResponse);
    }
}