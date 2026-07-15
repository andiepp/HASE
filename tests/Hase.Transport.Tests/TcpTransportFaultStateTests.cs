using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpTransportFaultStateTests
{
    [Fact]
    public async Task ExchangeAsync_PreCancelledToken_ShouldRemainConnected()
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

        Task<TcpClient> acceptedClientTask =
            listener.AcceptTcpClientAsync();

        using var client =
            new TcpClient();

        await client.ConnectAsync(
            IPAddress.Loopback,
            port);

        using TcpClient acceptedClient =
            await acceptedClientTask;

        await using var connection =
            new TcpTransportConnection(
                client,
                maximumPayloadLength: 1024);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        int notificationCount =
            0;

        connection.StateChanged +=
            (
                sender,
                eventArgs) =>
            {
                notificationCount++;
            };

        // Act
        Task Act()
        {
            return connection.ExchangeAsync(
                Array.Empty<byte>(),
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAsync<
            OperationCanceledException>(
                Act);

        Assert.Equal(
            TransportConnectionState.Connected,
            connection.State);

        Assert.Equal(
            0,
            notificationCount);
    }

    [Fact]
    public async Task ExchangeAsync_CancelledWhileWaitingForResponse_ShouldFault()
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

        var requestReceived =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseServer =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task serverTask =
            RunNonRespondingServerAsync(
                listener,
                requestReceived,
                releaseServer);

        using var client =
            new TcpClient();

        await client.ConnectAsync(
            IPAddress.Loopback,
            port);

        await using var connection =
            new TcpTransportConnection(
                client,
                maximumPayloadLength: 1024);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        var transitions =
            new List<(
                TransportConnectionState Previous,
                TransportConnectionState Current)>();

        connection.StateChanged +=
            (
                sender,
                eventArgs) =>
            {
                transitions.Add(
                    (
                        eventArgs.PreviousState,
                        eventArgs.CurrentState));
            };

        Task<byte[]> exchangeTask =
            connection.ExchangeAsync(
                [
                    0x01,
                    0x02,
                    0x03
                ],
                cancellationTokenSource.Token);

        await requestReceived.Task;

        // Act
        cancellationTokenSource.Cancel();

        // Assert
        await Assert.ThrowsAsync<
            OperationCanceledException>(
                async () =>
                {
                    await exchangeTask;
                });

        Assert.Equal(
            TransportConnectionState.Faulted,
            connection.State);

        Assert.Equal(
            [
                (
                    TransportConnectionState.Connected,
                    TransportConnectionState.Faulted)
            ],
            transitions);

        releaseServer.SetResult(
            true);

        await serverTask;
    }

    [Fact]
    public async Task ExchangeAsync_OversizedResponse_ShouldFaultAndRejectReuse()
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

        Task serverTask =
            RunOversizedResponseServerAsync(
                listener);

        using var client =
            new TcpClient();

        await client.ConnectAsync(
            IPAddress.Loopback,
            port);

        var connection =
            new TcpTransportConnection(
                client,
                maximumPayloadLength: 4);

        var transitions =
            new List<(
                TransportConnectionState Previous,
                TransportConnectionState Current)>();

        connection.StateChanged +=
            (
                sender,
                eventArgs) =>
            {
                transitions.Add(
                    (
                        eventArgs.PreviousState,
                        eventArgs.CurrentState));
            };

        // Act
        Task FirstExchange()
        {
            return connection.ExchangeAsync(
                [
                    0x10
                ]);
        }

        InvalidDataException exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                    FirstExchange);

        await serverTask;

        Task SecondExchange()
        {
            return connection.ExchangeAsync(
                [
                    0x20
                ]);
        }

        InvalidOperationException reuseException =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    SecondExchange);

        await connection.DisposeAsync();

        // Assert
        Assert.Equal(
            "The TCP frame payload length 5 exceeds "
            + "the configured maximum of 4 bytes.",
            exception.Message);

        Assert.Equal(
            "The TCP transport connection is faulted "
            + "and cannot be reused.",
            reuseException.Message);

        Assert.Equal(
            TransportConnectionState.Closed,
            connection.State);

        Assert.Equal(
            [
                (
                    TransportConnectionState.Connected,
                    TransportConnectionState.Faulted),
                (
                    TransportConnectionState.Faulted,
                    TransportConnectionState.Closed)
            ],
            transitions);
    }

    private static async Task RunNonRespondingServerAsync(
        TcpListener listener,
        TaskCompletionSource<bool> requestReceived,
        TaskCompletionSource<bool> releaseServer)
    {
        using TcpClient acceptedClient =
            await listener.AcceptTcpClientAsync();

        NetworkStream stream =
            acceptedClient.GetStream();

        await ReadFrameAsync(
            stream);

        requestReceived.SetResult(
            true);

        await releaseServer.Task;
    }

    private static async Task RunOversizedResponseServerAsync(
        TcpListener listener)
    {
        using TcpClient acceptedClient =
            await listener.AcceptTcpClientAsync();

        NetworkStream stream =
            acceptedClient.GetStream();

        await ReadFrameAsync(
            stream);

        byte[] responsePayload =
        [
            0x01,
            0x02,
            0x03,
            0x04,
            0x05
        ];

        byte[] responseFrame =
            TcpFrameCodec.Encode(
                responsePayload);

        await stream.WriteAsync(
            responseFrame);

        await stream.FlushAsync();
    }

    private static async Task<byte[]> ReadFrameAsync(
        NetworkStream stream)
    {
        byte[] header =
            new byte[
                TcpFrameCodec.HeaderLength];

        await stream.ReadExactlyAsync(
            header);

        uint payloadLength =
            BinaryPrimitives.ReadUInt32BigEndian(
                header);

        byte[] payload =
            new byte[
                checked((int)payloadLength)];

        await stream.ReadExactlyAsync(
            payload);

        return payload;
    }
}