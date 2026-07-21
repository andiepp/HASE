using Hase.CompactProtocol;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactEndpointConnectionOwnerTests
{
    [Fact]
    public async Task ReplaceAsync_FirstConnection_ShouldBecomeCurrent()
    {
        var owner =
            new CompactEndpointConnectionOwner();

        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        CompactEndpointConnection connection =
            CreateConnection(
                protocolConnection);

        await owner.ReplaceAsync(
            connection);

        Assert.Same(
            connection,
            owner.Current);

        Assert.Equal(
            0,
            protocolConnection.DisposeCallCount);

        await owner.DisposeAsync();
    }

    [Fact]
    public async Task ReplaceAsync_SecondConnection_ShouldDisposePrevious()
    {
        var owner =
            new CompactEndpointConnectionOwner();

        var firstProtocolConnection =
            new TestCompactSerialProtocolConnection();

        var secondProtocolConnection =
            new TestCompactSerialProtocolConnection();

        CompactEndpointConnection firstConnection =
            CreateConnection(
                firstProtocolConnection);

        CompactEndpointConnection secondConnection =
            CreateConnection(
                secondProtocolConnection);

        await owner.ReplaceAsync(
            firstConnection);

        await owner.ReplaceAsync(
            secondConnection);

        Assert.Same(
            secondConnection,
            owner.Current);

        Assert.Equal(
            1,
            firstProtocolConnection.DisposeCallCount);

        Assert.Equal(
            0,
            secondProtocolConnection.DisposeCallCount);

        await owner.DisposeAsync();
    }

    [Fact]
    public async Task ReplaceAsync_SameConnection_ShouldNotDispose()
    {
        var owner =
            new CompactEndpointConnectionOwner();

        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        CompactEndpointConnection connection =
            CreateConnection(
                protocolConnection);

        await owner.ReplaceAsync(
            connection);

        await owner.ReplaceAsync(
            connection);

        Assert.Same(
            connection,
            owner.Current);

        Assert.Equal(
            0,
            protocolConnection.DisposeCallCount);

        await owner.DisposeAsync();
    }

    [Fact]
    public async Task DetachAsync_ActiveConnection_ShouldClearAndDispose()
    {
        var owner =
            new CompactEndpointConnectionOwner();

        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        await owner.ReplaceAsync(
            CreateConnection(
                protocolConnection));

        await owner.DetachAsync();

        Assert.Null(
            owner.Current);

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);

        await owner.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_Repeated_ShouldDisposeConnectionOnce()
    {
        var owner =
            new CompactEndpointConnectionOwner();

        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        await owner.ReplaceAsync(
            CreateConnection(
                protocolConnection));

        await owner.DisposeAsync();

        await owner.DisposeAsync();

        Assert.Null(
            owner.Current);

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    [Fact]
    public async Task ReplaceAsync_AfterDispose_ShouldThrowWithoutOwningReplacement()
    {
        var owner =
            new CompactEndpointConnectionOwner();

        await owner.DisposeAsync();

        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        CompactEndpointConnection replacement =
            CreateConnection(
                protocolConnection);

        async Task Act()
        {
            await owner.ReplaceAsync(
                replacement);
        }

        await Assert.ThrowsAsync<ObjectDisposedException>(
            Act);

        Assert.Null(
            owner.Current);

        Assert.Equal(
            0,
            protocolConnection.DisposeCallCount);

        await replacement.DisposeAsync();
    }

    [Fact]
    public async Task ReplaceAsync_NullConnection_ShouldThrow()
    {
        var owner =
            new CompactEndpointConnectionOwner();

        async Task Act()
        {
            await owner.ReplaceAsync(
                null!);
        }

        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);

        await owner.DisposeAsync();
    }

    private static CompactEndpointConnection CreateConnection(
        ICompactSerialProtocolConnection protocolConnection)
    {
        return new CompactEndpointConnection(
            new EndpointDescriptor(
                new EndpointId(
                    "arduino-uno-01")),
            protocolConnection);
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