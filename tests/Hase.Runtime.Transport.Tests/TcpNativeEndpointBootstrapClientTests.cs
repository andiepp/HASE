using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport;
using Hase.Transport.Tcp;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class TcpNativeEndpointBootstrapClientTests
{
    [Fact]
    public async Task BootstrapAsync_Success_ShouldReturnResultAndDisposeConnection()
    {
        // Arrange
        NetworkEndpointConnectionDefinition definition =
            CreateDefinition();

        var endpointId =
            new EndpointId(
                "bootstrap-endpoint");

        var expectedResult =
            new NativeEndpointBootstrapResult(
                endpointId,
                new EndpointDescriptor(
                    endpointId));

        var connection =
            new TestTransportConnection();

        IRuntimeProtocolConnection? receivedProtocolConnection =
            null;

        EndpointId? receivedExpectedEndpointId =
            null;

        var bootstrapper =
            new TestBootstrapper(
                (
                    protocolConnection,
                    expectedEndpointId,
                    cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    receivedProtocolConnection =
                        protocolConnection;

                    receivedExpectedEndpointId =
                        expectedEndpointId;

                    return Task.FromResult(
                        expectedResult);
                });

        var client =
            new TcpNativeEndpointBootstrapClient(
                bootstrapper,
                (
                    receivedDefinition,
                    cancellationToken) =>
                {
                    Assert.Same(
                        definition,
                        receivedDefinition);

                    cancellationToken.ThrowIfCancellationRequested();

                    return Task.FromResult<ITransportConnection>(
                        connection);
                });

        // Act
        NativeEndpointBootstrapResult result =
            await client.BootstrapAsync(
                definition);

        // Assert
        Assert.Same(
            expectedResult,
            result);

        Assert.IsType<LegacyRuntimeProtocolConnection>(
            receivedProtocolConnection);

        Assert.Same(
            definition.ExpectedEndpointId,
            receivedExpectedEndpointId);

        Assert.Equal(
            1,
            connection.DisposeCallCount);
    }

    [Fact]
    public async Task BootstrapAsync_BootstrapFailure_ShouldDisposeConnection()
    {
        // Arrange
        var connection =
            new TestTransportConnection();

        var expectedException =
            new InvalidDataException(
                "Bootstrap failed.");

        var bootstrapper =
            new TestBootstrapper(
                (
                    protocolConnection,
                    expectedEndpointId,
                    cancellationToken) =>
                    Task.FromException<
                        NativeEndpointBootstrapResult>(
                            expectedException));

        var client =
            CreateClient(
                bootstrapper,
                connection);

        // Act
        InvalidDataException actualException =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => client.BootstrapAsync(
                    CreateDefinition()));

        // Assert
        Assert.Same(
            expectedException,
            actualException);

        Assert.Equal(
            1,
            connection.DisposeCallCount);
    }

    [Fact]
    public async Task BootstrapAsync_CallerCancellation_ShouldDisposeConnection()
    {
        // Arrange
        var connection =
            new TestTransportConnection();

        var bootstrapper =
            new TestBootstrapper(
                async (
                    protocolConnection,
                    expectedEndpointId,
                    cancellationToken) =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "The cancellation wait completed unexpectedly.");
                });

        var client =
            CreateClient(
                bootstrapper,
                connection);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task<NativeEndpointBootstrapResult> bootstrapTask =
            client.BootstrapAsync(
                CreateDefinition(),
                cancellationTokenSource.Token);

        // Act
        cancellationTokenSource.Cancel();

        // Assert
        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => bootstrapTask);

        Assert.Equal(
            cancellationTokenSource.Token,
            exception.CancellationToken);

        Assert.Equal(
            1,
            connection.DisposeCallCount);
    }

    [Fact]
    public async Task BootstrapAsync_ConnectionFailure_ShouldNotInvokeBootstrapper()
    {
        // Arrange
        var expectedException =
            new IOException(
                "Connection failed.");

        var bootstrapper =
            new TestBootstrapper(
                (
                    protocolConnection,
                    expectedEndpointId,
                    cancellationToken) =>
                    throw new InvalidOperationException(
                        "The bootstrapper must not be called."));

        var client =
            new TcpNativeEndpointBootstrapClient(
                bootstrapper,
                (
                    definition,
                    cancellationToken) =>
                    Task.FromException<ITransportConnection>(
                        expectedException));

        // Act
        IOException actualException =
            await Assert.ThrowsAsync<IOException>(
                () => client.BootstrapAsync(
                    CreateDefinition()));

        // Assert
        Assert.Same(
            expectedException,
            actualException);

        Assert.Equal(
            0,
            bootstrapper.CallCount);
    }

    [Fact]
    public async Task BootstrapAsync_NullDefinition_ShouldThrow()
    {
        // Arrange
        var bootstrapper =
            new TestBootstrapper(
                (
                    protocolConnection,
                    expectedEndpointId,
                    cancellationToken) =>
                    throw new InvalidOperationException(
                        "The bootstrapper must not be called."));

        var client =
            new TcpNativeEndpointBootstrapClient(
                bootstrapper,
                (
                    definition,
                    cancellationToken) =>
                    throw new InvalidOperationException(
                        "The connection factory must not be called."));

        // Act
        Task Act()
        {
            return client.BootstrapAsync(
                null!);
        }

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullBootstrapper_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new TcpNativeEndpointBootstrapClient(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NegativeMaximumPayloadLength_ShouldThrow()
    {
        // Arrange
        var bootstrapper =
            new TestBootstrapper(
                (
                    protocolConnection,
                    expectedEndpointId,
                    cancellationToken) =>
                    throw new InvalidOperationException(
                        "The bootstrapper must not be called."));

        // Act
        void Act()
        {
            _ = new TcpNativeEndpointBootstrapClient(
                bootstrapper,
                maximumPayloadLength: -1);
        }

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public async Task BootstrapAsync_NullConnection_ShouldThrow()
    {
        // Arrange
        var bootstrapper =
            new TestBootstrapper(
                (
                    protocolConnection,
                    expectedEndpointId,
                    cancellationToken) =>
                    throw new InvalidOperationException(
                        "The bootstrapper must not be called."));

        var client =
            new TcpNativeEndpointBootstrapClient(
                bootstrapper,
                (
                    definition,
                    cancellationToken) =>
                    Task.FromResult<ITransportConnection>(
                        null!));

        // Act
        Task Act()
        {
            return client.BootstrapAsync(
                CreateDefinition());
        }

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            Act);

        Assert.Equal(
            0,
            bootstrapper.CallCount);
    }

    private static TcpNativeEndpointBootstrapClient CreateClient(
        INativeEndpointBootstrapper bootstrapper,
        ITransportConnection connection)
    {
        return new TcpNativeEndpointBootstrapClient(
            bootstrapper,
            (
                definition,
                cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                return Task.FromResult(
                    connection);
            });
    }

    private static NetworkEndpointConnectionDefinition CreateDefinition()
    {
        return NetworkEndpointConnectionDefinition
            .FromConfiguration(
                new TcpTransportOptions(
                    "192.168.0.223",
                    5000),
                new EndpointId(
                    "bootstrap-endpoint"));
    }

    private sealed class TestBootstrapper
        : INativeEndpointBootstrapper
    {
        private readonly Func<
            IRuntimeProtocolConnection,
            EndpointId?,
            CancellationToken,
            Task<NativeEndpointBootstrapResult>> _bootstrapAsync;

        public TestBootstrapper(
            Func<
                IRuntimeProtocolConnection,
                EndpointId?,
                CancellationToken,
                Task<NativeEndpointBootstrapResult>> bootstrapAsync)
        {
            _bootstrapAsync =
                bootstrapAsync;
        }

        public int CallCount
        {
            get;
            private set;
        }

        public Task<NativeEndpointBootstrapResult> BootstrapAsync(
            IRuntimeProtocolConnection connection,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            return _bootstrapAsync(
                connection,
                expectedEndpointId,
                cancellationToken);
        }
    }

    private sealed class TestTransportConnection
        : ITransportConnection,
          IAsyncDisposable
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

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The test bootstrapper does not exchange protocol frames.");
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            return ValueTask.CompletedTask;
        }
    }
}