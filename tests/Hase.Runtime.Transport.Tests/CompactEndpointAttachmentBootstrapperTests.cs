using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactEndpointAttachmentBootstrapperTests
{
    [Fact]
    public void Constructor_NullConnectionFactory_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactEndpointAttachmentBootstrapper(
                (ICompactEndpointConnectionFactory)null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task BootstrapAsync_InitializedConnection_ShouldReturnResultAndDisposeConnection()
    {
        // Arrange
        EndpointId endpointId =
            new(
                "arduino-uno-01");

        DescriptorReference descriptorReference =
            new(
                new DescriptorId(
                    "arduino-uno-validation"),
                version: 1);

        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        var initializationResult =
            new CompactEndpointInitializationResult(
                endpointId,
                descriptorReference,
                descriptorDefinition,
                descriptorDefinition.Materialize(
                    endpointId));

        var protocolConnection =
            new StubCompactSerialProtocolConnection();

        var connectionFactory =
            new StubCompactEndpointConnectionFactory(
                new CompactEndpointConnection(
                    protocolConnection,
                    initializationResult));

        var bootstrapper =
            new CompactEndpointAttachmentBootstrapper(
                connectionFactory);

        SerialEndpointConnectionDefinition connectionDefinition =
            SerialEndpointConnectionDefinition.FromConfiguration(
                new SerialTransportOptions(
                    "COM10",
                    115200),
                endpointId);

        // Act
        CompactEndpointAttachmentBootstrapResult result =
            await bootstrapper.BootstrapAsync(
                connectionDefinition);

        // Assert
        Assert.Same(
            initializationResult.EndpointId,
            result.EndpointId);

        Assert.Same(
            initializationResult.DescriptorReference,
            result.DescriptorReference);

        Assert.Same(
            initializationResult.DescriptorDefinition,
            result.DescriptorDefinition);

        Assert.Same(
            initializationResult.Descriptor,
            result.Descriptor);

        Assert.Same(
            connectionDefinition.TransportOptions,
            connectionFactory.TransportOptions);

        Assert.Same(
            endpointId,
            connectionFactory.ExpectedEndpointId);

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    [Fact]
    public async Task BootstrapAsync_LegacyConnection_ShouldDisposeAndThrow()
    {
        // Arrange
        var protocolConnection =
            new StubCompactSerialProtocolConnection();

        var connection =
            new CompactEndpointConnection(
                new EndpointDescriptor(
                    new EndpointId(
                        "arduino-uno-01")),
                protocolConnection);

        var bootstrapper =
            new CompactEndpointAttachmentBootstrapper(
                new StubCompactEndpointConnectionFactory(
                    connection));

        // Act
        Task Act()
        {
            return bootstrapper.BootstrapAsync(
                CreateConnectionDefinition());
        }

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            Act);

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    [Fact]
    public async Task BootstrapAsync_PreCancelledToken_ShouldNotConnect()
    {
        // Arrange
        var connectionFactory =
            new CountingCompactEndpointConnectionFactory();

        var bootstrapper =
            new CompactEndpointAttachmentBootstrapper(
                connectionFactory);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        Task Act()
        {
            return bootstrapper.BootstrapAsync(
                CreateConnectionDefinition(),
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            0,
            connectionFactory.CallCount);
    }

    private static SerialEndpointConnectionDefinition
        CreateConnectionDefinition()
    {
        return SerialEndpointConnectionDefinition.FromConfiguration(
            new SerialTransportOptions(
                "COM10",
                115200));
    }

    private sealed class StubCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        private readonly CompactEndpointConnection _connection;

        public StubCompactEndpointConnectionFactory(
            CompactEndpointConnection connection)
        {
            _connection =
                connection;
        }

        public SerialTransportOptions? TransportOptions
        {
            get;
            private set;
        }

        public EndpointId? ExpectedEndpointId
        {
            get;
            private set;
        }

        public Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TransportOptions =
                transportOptions;

            ExpectedEndpointId =
                expectedEndpointId;

            return Task.FromResult(
                _connection);
        }
    }

    private sealed class CountingCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        public int CallCount
        {
            get;
            private set;
        }

        public Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            throw new InvalidOperationException(
                "A compact connection was not expected.");
        }
    }

    private sealed class StubCompactSerialProtocolConnection
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