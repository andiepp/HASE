using System.Net;
using System.Net.Sockets;
using Hase.Transport.Loopback;
using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TransportConnectionStateTests
{
    [Fact]
    public void StateChangedEventArgs_ShouldPreserveStates()
    {
        // Arrange
        TransportConnectionState previousState =
            TransportConnectionState.Connected;

        TransportConnectionState currentState =
            TransportConnectionState.Closed;

        // Act
        var eventArgs =
            new TransportConnectionStateChangedEventArgs(
                previousState,
                currentState);

        // Assert
        Assert.Equal(
            previousState,
            eventArgs.PreviousState);

        Assert.Equal(
            currentState,
            eventArgs.CurrentState);
    }

    [Fact]
    public void StateChangedEventArgs_EqualStates_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new TransportConnectionStateChangedEventArgs(
                TransportConnectionState.Connected,
                TransportConnectionState.Connected);
        }

        // Assert
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                Act);

        Assert.Equal(
            "currentState",
            exception.ParamName);
    }

    [Fact]
    public void LoopbackConnection_State_ShouldBeConnected()
    {
        // Arrange
        var connection =
            CreateLoopbackConnection();

        // Act
        TransportConnectionState state =
            connection.State;

        // Assert
        Assert.Equal(
            TransportConnectionState.Connected,
            state);
    }

    [Fact]
    public async Task LoopbackConnection_Exchange_ShouldNotRaiseStateChanged()
    {
        // Arrange
        var connection =
            CreateLoopbackConnection();

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
        byte[] response =
            await connection.ExchangeAsync(
                [
                    0x01,
                    0x02
                ]);

        // Assert
        Assert.Equal(
            [
                0x01,
                0x02
            ],
            response);

        Assert.Equal(
            0,
            notificationCount);
    }

    [Fact]
    public async Task TcpConnection_StateBeforeDisposal_ShouldBeConnected()
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
        TransportConnectionState state =
            connection.State;

        // Assert
        Assert.Equal(
            TransportConnectionState.Connected,
            state);
    }

    [Fact]
    public async Task TcpConnection_StateAfterDisposal_ShouldBeClosed()
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

        // Act
        await connection.DisposeAsync();

        TransportConnectionState state =
            connection.State;

        // Assert
        Assert.Equal(
            TransportConnectionState.Closed,
            state);
    }

    [Fact]
    public async Task TcpConnection_Disposal_ShouldRaiseStateChanged()
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

        object? actualSender =
            null;

        TransportConnectionStateChangedEventArgs? actualEventArgs =
            null;

        int notificationCount =
            0;

        connection.StateChanged +=
            (
                sender,
                eventArgs) =>
            {
                notificationCount++;

                actualSender =
                    sender;

                actualEventArgs =
                    eventArgs;
            };

        // Act
        await connection.DisposeAsync();

        // Assert
        Assert.Equal(
            1,
            notificationCount);

        Assert.Same(
            connection,
            actualSender);

        Assert.NotNull(
            actualEventArgs);

        Assert.Equal(
            TransportConnectionState.Connected,
            actualEventArgs.PreviousState);

        Assert.Equal(
            TransportConnectionState.Closed,
            actualEventArgs.CurrentState);
    }

    [Fact]
    public async Task TcpConnection_RepeatedDisposal_ShouldRaiseStateChangedOnce()
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
        await connection.DisposeAsync();
        await connection.DisposeAsync();

        // Assert
        Assert.Equal(
            1,
            notificationCount);

        Assert.Equal(
            TransportConnectionState.Closed,
            connection.State);
    }

    private static LoopbackTransportConnection
        CreateLoopbackConnection()
    {
        return new LoopbackTransportConnection(
            static (
                request,
                cancellationToken) =>
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                return Task.FromResult(
                    request);
            });
    }
}