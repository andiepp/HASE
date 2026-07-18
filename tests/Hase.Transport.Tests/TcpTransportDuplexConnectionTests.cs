using System.Net;
using System.Net.Sockets;
using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpTransportDuplexConnectionTests
{
    [Fact]
    public async Task SendAsync_ShouldWriteFramedPayload()
    {
        // Arrange
        byte[] expectedPayload =
        [
            0x10,
            0x20,
            0x30
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
                maximumPayloadLength:
                    1024);

        // Act
        await connection.SendAsync(
            expectedPayload);

        byte[] actualPayload =
            await TcpFrameReader.ReadAsync(
                serverStream,
                maximumPayloadLength:
                    1024);

        // Assert
        Assert.Equal(
            expectedPayload,
            actualPayload);

        Assert.Equal(
            TransportConnectionState.Connected,
            connection.State);
    }

    [Fact]
    public async Task ReceiveAsync_ShouldReadFramedPayload()
    {
        // Arrange
        byte[] expectedPayload =
        [
            0x40,
            0x50,
            0x60
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
                maximumPayloadLength:
                    1024);

        byte[] frame =
            TcpFrameCodec.Encode(
                expectedPayload);

        await serverStream.WriteAsync(
            frame.AsMemory());

        await serverStream.FlushAsync();

        // Act
        byte[] actualPayload =
            await connection.ReceiveAsync();

        // Assert
        Assert.Equal(
            expectedPayload,
            actualPayload);

        Assert.Equal(
            TransportConnectionState.Connected,
            connection.State);
    }

    [Fact]
    public async Task SendAndReceiveAsync_ShouldProgressConcurrently()
    {
        // Arrange
        byte[] expectedSentPayload =
        [
            0x01,
            0x02
        ];

        byte[] expectedReceivedPayload =
        [
            0xA1,
            0xA2,
            0xA3
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
                maximumPayloadLength:
                    1024);

        Task<byte[]> receiveTask =
            connection.ReceiveAsync();

        // Act
        await connection.SendAsync(
            expectedSentPayload);

        byte[] actualSentPayload =
            await TcpFrameReader.ReadAsync(
                serverStream,
                maximumPayloadLength:
                    1024);

        byte[] receivedFrame =
            TcpFrameCodec.Encode(
                expectedReceivedPayload);

        await serverStream.WriteAsync(
            receivedFrame.AsMemory());

        await serverStream.FlushAsync();

        byte[] actualReceivedPayload =
            await receiveTask;

        // Assert
        Assert.Equal(
            expectedSentPayload,
            actualSentPayload);

        Assert.Equal(
            expectedReceivedPayload,
            actualReceivedPayload);

        Assert.Equal(
            TransportConnectionState.Connected,
            connection.State);
    }

    [Fact]
    public async Task ReceiveAsync_SecondActiveReceive_ShouldThrow()
    {
        // Arrange
        byte[] expectedPayload =
        [
            0x71,
            0x72
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
                maximumPayloadLength:
                    1024);

        Task<byte[]> firstReceiveTask =
            connection.ReceiveAsync();

        // Act
        Task<byte[]> SecondReceive()
        {
            return connection.ReceiveAsync();
        }

        InvalidOperationException exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    SecondReceive);

        byte[] frame =
            TcpFrameCodec.Encode(
                expectedPayload);

        await serverStream.WriteAsync(
            frame.AsMemory());

        await serverStream.FlushAsync();

        byte[] firstPayload =
            await firstReceiveTask;

        // Assert
        Assert.Equal(
            "Only one receive operation may be active "
            + "for a TCP transport connection.",
            exception.Message);

        Assert.Equal(
            expectedPayload,
            firstPayload);

        Assert.Equal(
            TransportConnectionState.Connected,
            connection.State);
    }
}