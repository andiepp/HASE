using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Transport;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactEndpointConnectionTests
{
    [Fact]
    public void Constructor_ValidArguments_ShouldRetainDescriptorAndConnection()
    {
        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "uno-01"));

        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        var connection =
            new CompactEndpointConnection(
                descriptor,
                protocolConnection);

        Assert.Same(
            descriptor,
            connection.Descriptor);

        Assert.Same(
            protocolConnection,
            connection.Connection);
    }

    [Fact]
    public void Constructor_NullDescriptor_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEndpointConnection(
                null!,
                new TestCompactSerialProtocolConnection());
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullConnection_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEndpointConnection(
                new EndpointDescriptor(
                    new EndpointId(
                        "uno-01")),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeOwnedProtocolConnection()
    {
        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        var connection =
            new CompactEndpointConnection(
                new EndpointDescriptor(
                    new EndpointId(
                        "uno-01")),
                protocolConnection);

        await connection.DisposeAsync();

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    [Fact]
    public async Task DisposeAsync_RepeatedCall_ShouldRemainSafe()
    {
        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        var connection =
            new CompactEndpointConnection(
                new EndpointDescriptor(
                    new EndpointId(
                        "uno-01")),
                protocolConnection);

        await connection.DisposeAsync();
        await connection.DisposeAsync();

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    private sealed class TestCompactSerialProtocolConnection
        : ICompactSerialProtocolConnection
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

        public int DisposeCallCount
        {
            get;
            private set;
        }

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void Invalidate()
        {
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            return ValueTask.CompletedTask;
        }
    }
}