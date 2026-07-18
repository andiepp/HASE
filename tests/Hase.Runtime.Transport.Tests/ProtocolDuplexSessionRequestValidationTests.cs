using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolDuplexSessionRequestValidationTests
{
    [Fact]
    public void Constructor_NullConnection_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new ProtocolDuplexSession(
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<
                ArgumentNullException>(
                    Act);

        Assert.Equal(
            "connection",
            exception.ParamName);
    }

    [Fact]
    public async Task SendAsync_NonRequestMessage_ShouldThrow()
    {
        // Arrange
        var session =
            new ProtocolDuplexSession(
                new TestDuplexTransportConnection());

        var response =
            new DiscoverResponse(
                new CorrelationId(
                    401),
                new EndpointId(
                    "Endpoint-1"),
                []);

        // Act
        Task<ProtocolMessage> Act()
        {
            return session.SendAsync(
                response);
        }

        // Assert
        ArgumentException exception =
            await Assert.ThrowsAsync<
                ArgumentException>(
                    Act);

        Assert.Equal(
            "request",
            exception.ParamName);

        Assert.Equal(
            "Only request-role protocol messages can be sent "
            + "through a protocol duplex session. "
            + "(Parameter 'request')",
            exception.Message);
    }

    [Fact]
    public async Task SendAsync_ZeroCorrelationId_ShouldThrow()
    {
        // Arrange
        var session =
            new ProtocolDuplexSession(
                new TestDuplexTransportConnection());

        var request =
            new DiscoverRequest(
                CorrelationId.None);

        // Act
        Task<ProtocolMessage> Act()
        {
            return session.SendAsync(
                request);
        }

        // Assert
        ArgumentException exception =
            await Assert.ThrowsAsync<
                ArgumentException>(
                    Act);

        Assert.Equal(
            "request",
            exception.ParamName);

        Assert.Equal(
            "A duplex protocol request must have a nonzero "
            + "correlation identifier. "
            + "(Parameter 'request')",
            exception.Message);
    }

    private sealed class TestDuplexTransportConnection
        : ITransportDuplexConnection
    {
        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged
        {
            add
            {
            }

            remove
            {
            }
        }

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SendAsync(
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}