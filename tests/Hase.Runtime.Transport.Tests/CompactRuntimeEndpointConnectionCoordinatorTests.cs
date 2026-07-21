using Hase.CompactProtocol;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactRuntimeEndpointConnectionCoordinatorTests
{
    private static readonly EndpointId EndpointId =
        new(
            "arduino-uno-01");

    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly PropertyId PropertyId =
        new(
            "led-state");

    [Fact]
    public async Task ConnectAsync_Success_ShouldSynchronizeAndBecomeReady()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        CompactEndpointConnection candidate =
            CreateConnection(
                definition,
                protocolConnection);

        var connectionFactory =
            new TestCompactEndpointConnectionFactory(
                candidate);

        var coordinator =
            CreateCoordinator(
                connectionFactory,
                definition,
                runtimeEndpoint);

        await coordinator.ConnectAsync();

        Assert.Equal(
            EndpointId,
            connectionFactory.ExpectedEndpointId);

        Assert.Equal(
            1,
            connectionFactory.ConnectCallCount);

        Assert.Same(
            candidate,
            coordinator.ActiveConnection);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        RuntimeProperty runtimeProperty =
            runtimeEndpoint
                .FindInstrument(
                    InstrumentId)!
                .FindProperty(
                    PropertyId)!;

        Assert.NotNull(
            runtimeProperty.CurrentValue);

        Assert.True(
            Assert.IsType<bool>(
                runtimeProperty.CurrentValue.Value));

        Assert.Equal(
            PropertyQuality.Good,
            runtimeProperty.CurrentValue.Quality);

        Assert.Equal(
            1,
            protocolConnection.ExchangeCallCount);

        await coordinator.DisposeAsync();
    }

    [Fact]
    public async Task ConnectAsync_FactoryFailure_ShouldBecomeFaulted()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var expectedException =
            new IOException(
                "The configured serial port is unavailable.");

        var connectionFactory =
            new TestCompactEndpointConnectionFactory(
                expectedException);

        var coordinator =
            CreateCoordinator(
                connectionFactory,
                definition,
                runtimeEndpoint);

        async Task Act()
        {
            await coordinator.ConnectAsync();
        }

        IOException exception =
            await Assert.ThrowsAsync<IOException>(
                Act);

        Assert.Same(
            expectedException,
            exception);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Null(
            coordinator.ActiveConnection);

        Assert.Equal(
            1,
            connectionFactory.ConnectCallCount);

        await coordinator.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_ActiveConnection_ShouldDisposeAndDisconnect()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        var connectionFactory =
            new TestCompactEndpointConnectionFactory(
                CreateConnection(
                    definition,
                    protocolConnection));

        var coordinator =
            CreateCoordinator(
                connectionFactory,
                definition,
                runtimeEndpoint);

        await coordinator.ConnectAsync();

        await coordinator.DisposeAsync();

        Assert.Null(
            coordinator.ActiveConnection);

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        await coordinator.DisposeAsync();

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    private static CompactRuntimeEndpointConnectionCoordinator
        CreateCoordinator(
            ICompactEndpointConnectionFactory connectionFactory,
            EndpointDescriptorDefinition definition,
            RuntimeEndpoint runtimeEndpoint)
    {
        return new CompactRuntimeEndpointConnectionCoordinator(
            connectionFactory,
            new SerialTransportOptions(
                "COM10",
                115200),
            CreatePropertyMap(
                definition),
            runtimeEndpoint,
            new EndpointDescriptorCompatibilityValidator());
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint(
        EndpointDescriptorDefinition definition)
    {
        return new RuntimeContext()
            .AddEndpoint(
                definition.Materialize(
                    EndpointId));
    }

    private static CompactEndpointConnection CreateConnection(
        EndpointDescriptorDefinition definition,
        ICompactSerialProtocolConnection protocolConnection)
    {
        return new CompactEndpointConnection(
            definition.Materialize(
                EndpointId),
            protocolConnection);
    }

    private static EndpointDescriptorDefinition CreateDefinition()
    {
        var property =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath(
                    "Led",
                    "State"),
                "Built-in LED State",
                new BooleanDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.Read
            };

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Arduino Uno GPIO Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            property
                        ])
            };

        return new EndpointDescriptorDefinition(
            metadata:
                new EndpointMetadata
                {
                    DisplayName =
                        "Arduino Uno Compact Endpoint"
                },
            instruments:
            [
                instrument
            ]);
    }

    private static CompactPropertyMap CreatePropertyMap(
        EndpointDescriptorDefinition definition)
    {
        return new CompactPropertyMap(
            definition,
            mappings:
            [
                new CompactPropertyMapping(
                    compactPropertyId: 0x01,
                    InstrumentId,
                    PropertyId,
                    CompactPropertyValueEncoding.Boolean)
            ]);
    }

    private sealed class TestCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        private readonly CompactEndpointConnection? _connection;
        private readonly Exception? _exception;

        public TestCompactEndpointConnectionFactory(
            CompactEndpointConnection connection)
        {
            _connection =
                connection;
        }

        public TestCompactEndpointConnectionFactory(
            Exception exception)
        {
            _exception =
                exception;
        }

        public int ConnectCallCount
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

            ConnectCallCount++;

            ExpectedEndpointId =
                expectedEndpointId;

            if (_exception is not null)
            {
                return Task.FromException<
                    CompactEndpointConnection>(
                        _exception);
            }

            return Task.FromResult(
                _connection
                ?? throw new InvalidOperationException(
                    "No compact endpoint connection was configured."));
        }
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

        public int ExchangeCallCount
        {
            get;
            private set;
        }

        public int DisposeCallCount
        {
            get;
            private set;
        }

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ExchangeCallCount++;

            CompactReadPropertyRequest readRequest =
                CompactReadPropertyCodec.DecodeRequest(
                    request);

            return Task.FromResult(
                CompactReadPropertyCodec.EncodeResponse(
                    new CompactReadPropertyResponse(
                        readRequest.CorrelationId,
                        readRequest.PropertyId,
                        CompactPropertyReadStatus.Success,
                        value: new byte[]
                        {
                            0x01
                        })));
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