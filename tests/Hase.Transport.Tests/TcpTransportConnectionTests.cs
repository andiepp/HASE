using System.Net;
using System.Net.Sockets;
using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpTransportConnectionTests
{
    [Fact]
    public async Task ExchangeAsync_ShouldSendRequestAndReturnResponse()
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

        await using var connection =
            new TcpTransportConnection(
                client,
                maximumPayloadLength: 1024);

        // Act
        byte[] actualResponse =
            await connection.ExchangeAsync(
                expectedRequest);

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
    public async Task ExchangeAsync_NullRequest_ShouldThrow()
    {
        // Arrange
        await using var server =
            new FramedTcpTestServer();

        using var client =
            new TcpClient();

        await client.ConnectAsync(
            IPAddress.Loopback,
            server.Port);

        await using var connection =
            new TcpTransportConnection(
                client,
                maximumPayloadLength: 1024);

        // Act
        Task Act()
        {
            return connection.ExchangeAsync(
                null!);
        }

        // Assert
        await Assert.ThrowsAsync<
            ArgumentNullException>(
                Act);
    }

    [Fact]
    public async Task ExchangeAsync_ResponseExceedsMaximum_ShouldThrow()
    {
        // Arrange
        await using var server =
            new FramedTcpTestServer();

        Task serverTask =
            server.ServeSingleExchangeAsync(
                static (
                    request,
                    cancellationToken) =>
                {
                    return Task.FromResult(
                        new byte[5]);
                });

        using var client =
            new TcpClient();

        await client.ConnectAsync(
            IPAddress.Loopback,
            server.Port);

        await using var connection =
            new TcpTransportConnection(
                client,
                maximumPayloadLength: 4);

        // Act
        Task Act()
        {
            return connection.ExchangeAsync(
                Array.Empty<byte>());
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                    Act);

        await serverTask;

        Assert.Equal(
            "The TCP frame payload length 5 exceeds "
            + "the configured maximum of 4 bytes.",
            exception.Message);
    }

    [Fact]
    public async Task ExchangeAsync_AfterDispose_ShouldThrow()
    {
        // Arrange
        await using var server =
            new FramedTcpTestServer();

        using var client =
            new TcpClient();

        await client.ConnectAsync(
            IPAddress.Loopback,
            server.Port);

        var connection =
            new TcpTransportConnection(
                client,
                maximumPayloadLength: 1024);

        await connection.DisposeAsync();

        // Act
        Task Act()
        {
            return connection.ExchangeAsync(
                Array.Empty<byte>());
        }

        // Assert
        await Assert.ThrowsAsync<
            ObjectDisposedException>(
                Act);
    }

    [Fact]
    public async Task Invalidate_ConnectedConnection_ShouldTransitionToFaulted()
    {
        // Arrange
        await using var server =
            new FramedTcpTestServer();

        using var client =
            new TcpClient();

        await client.ConnectAsync(
            IPAddress.Loopback,
            server.Port);

        await using var connection =
            new TcpTransportConnection(
                client,
                maximumPayloadLength: 1024);

        var stateChanges =
            new List<TransportConnectionStateChangedEventArgs>();

        connection.StateChanged +=
            (
                sender,
                eventArgs) =>
            {
                stateChanges.Add(
                    eventArgs);
            };

        // Act
        ITransportConnectionInvalidator invalidator =
            Assert.IsAssignableFrom<
                ITransportConnectionInvalidator>(
                    connection);

        invalidator.Invalidate();

        // Assert
        Assert.Equal(
            TransportConnectionState.Faulted,
            connection.State);

        TransportConnectionStateChangedEventArgs stateChange =
            Assert.Single(
                stateChanges);

        Assert.Equal(
            TransportConnectionState.Connected,
            stateChange.PreviousState);

        Assert.Equal(
            TransportConnectionState.Faulted,
            stateChange.CurrentState);
    }

    [Fact]
    public void Constructor_NullClient_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new TcpTransportConnection(
                null!,
                maximumPayloadLength: 1024);
        }

        // Assert
        Assert.Throws<
            ArgumentNullException>(
                Act);
    }

    [Fact]
    public async Task Constructor_NegativeMaximumPayloadLength_ShouldThrow()
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

        // Act
        void Act()
        {
            _ = new TcpTransportConnection(
                client,
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
    }
}