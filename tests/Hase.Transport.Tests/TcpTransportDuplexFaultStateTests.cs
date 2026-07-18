using System.Net;
using System.Net.Sockets;
using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpTransportDuplexFaultStateTests
{
    [Fact]
    public async Task ReceiveAsync_PreCancelledToken_ShouldNotFaultConnection()
    {
        // Arrange
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

        await using var connection =
            new TcpTransportConnection(
                client,
                maximumPayloadLength:
                    1024);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        Task<byte[]> Act()
        {
            return connection.ReceiveAsync(
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                Act);

        Assert.Equal(
            TransportConnectionState.Connected,
            connection.State);
    }

    [Fact]
    public async Task ReceiveAsync_CancelledWhileWaiting_ShouldFaultConnection()
    {
        // Arrange
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

        await using var connection =
            new TcpTransportConnection(
                client,
                maximumPayloadLength:
                    1024);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task<byte[]> receiveTask =
            connection.ReceiveAsync(
                cancellationTokenSource.Token);

        // Act
        cancellationTokenSource.Cancel();

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await receiveTask);

        Assert.Equal(
            TransportConnectionState.Faulted,
            connection.State);
    }

    [Fact]
    public async Task ReceiveAsync_InvalidFrame_ShouldFaultConnection()
    {
        // Arrange
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
                maximumPayloadLength:
                    2);

        byte[] oversizedPayload =
        [
            0x01,
            0x02,
            0x03
        ];

        byte[] oversizedFrame =
            TcpFrameCodec.Encode(
                oversizedPayload);

        await serverStream.WriteAsync(
            oversizedFrame.AsMemory());

        await serverStream.FlushAsync();

        // Act
        Task<byte[]> Act()
        {
            return connection.ReceiveAsync();
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                    Act);

        Assert.Equal(
            "The TCP frame payload length 3 exceeds "
            + "the configured maximum of 2 bytes.",
            exception.Message);

        Assert.Equal(
            TransportConnectionState.Faulted,
            connection.State);
    }
}