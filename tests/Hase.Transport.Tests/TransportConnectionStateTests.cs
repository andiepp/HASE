using System.Net;
using System.Net.Sockets;
using Hase.Transport.Loopback;
using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TransportConnectionStateTests
{
    [Fact]
    public void LoopbackConnection_State_ShouldBeConnected()
    {
        // Arrange
        var connection =
            new LoopbackTransportConnection(
                static (
                    request,
                    cancellationToken) =>
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    return Task.FromResult(
                        request);
                });

        // Act
        TransportConnectionState state =
            connection.State;

        // Assert
        Assert.Equal(
            TransportConnectionState.Connected,
            state);
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
}