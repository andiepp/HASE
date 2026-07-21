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

public sealed class CompactFaultedConnectionDetachmentTests
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
    public async Task DetachFaultedConnectionAsync_ShouldDisposeConnectionAndPreserveCache()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        CompactRuntimeEndpointConnectionCoordinator coordinator =
            CreateCoordinator(
                definition,
                runtimeEndpoint,
                protocolConnection);

        await coordinator.ConnectAsync();

        RuntimeProperty runtimeProperty =
            FindRuntimeProperty(
                runtimeEndpoint);

        PropertyValue cachedValue =
            runtimeProperty.CurrentValue
            ?? throw new InvalidOperationException(
                "Initial synchronization did not populate the cache.");

        coordinator.MarkFaulted(
            "The compact serial connection was lost.");

        await coordinator.DetachFaultedConnectionAsync();

        Assert.Null(
            coordinator.ActiveConnection);

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);

        Assert.Same(
            cachedValue,
            runtimeProperty.CurrentValue);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            runtimeEndpoint.ConnectionStatus.State);

        await coordinator.DisposeAsync();

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    [Fact]
    public async Task DetachFaultedConnectionAsync_NoActiveConnection_ShouldRemainFaulted()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var connectionFactory =
            new TestCompactEndpointConnectionFactory(
                connection: null);

        var coordinator =
            new CompactRuntimeEndpointConnectionCoordinator(
                connectionFactory,
                new SerialTransportOptions(
                    "COM10",
                    115200),
                CreatePropertyMap(
                    definition),
                runtimeEndpoint,
                new EndpointDescriptorCompatibilityValidator());

        coordinator.MarkFaulted(
            "Initial connection failed.");

        await coordinator.DetachFaultedConnectionAsync();

        Assert.Null(
            coordinator.ActiveConnection);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            runtimeEndpoint.ConnectionStatus.State);

        await coordinator.DisposeAsync();
    }

    [Fact]
    public async Task DetachFaultedConnectionAsync_ReadyEndpoint_ShouldThrow()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        CompactRuntimeEndpointConnectionCoordinator coordinator =
            CreateCoordinator(
                definition,
                runtimeEndpoint,
                protocolConnection);

        await coordinator.ConnectAsync();

        async Task Act()
        {
            await coordinator.DetachFaultedConnectionAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            Act);

        Assert.NotNull(
            coordinator.ActiveConnection);

        Assert.Equal(
            0,
            protocolConnection.DisposeCallCount);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        await coordinator.DisposeAsync();
    }

    [Fact]
    public async Task DetachFaultedConnectionAsync_PreCancelled_ShouldNotDetach()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        CompactRuntimeEndpointConnectionCoordinator coordinator =
            CreateCoordinator(
                definition,
                runtimeEndpoint,
                protocolConnection);

        await coordinator.ConnectAsync();

        coordinator.MarkFaulted(
            "The compact serial connection was lost.");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            await coordinator.DetachFaultedConnectionAsync(
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.NotNull(
            coordinator.ActiveConnection);

        Assert.Equal(
            0,
            protocolConnection.DisposeCallCount);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            runtimeEndpoint.ConnectionStatus.State);

        await coordinator.DisposeAsync();
    }

    private static CompactRuntimeEndpointConnectionCoordinator
        CreateCoordinator(
            EndpointDescriptorDefinition definition,
            RuntimeEndpoint runtimeEndpoint,
            ICompactSerialProtocolConnection protocolConnection)
    {
        var connection =
            new CompactEndpointConnection(
                definition.Materialize(
                    EndpointId),
                protocolConnection);

        return new CompactRuntimeEndpointConnectionCoordinator(
            new TestCompactEndpointConnectionFactory(
                connection),
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

    private static RuntimeProperty FindRuntimeProperty(
        RuntimeEndpoint runtimeEndpoint)
    {
        return runtimeEndpoint
            .FindInstrument(
                InstrumentId)!
            .FindProperty(
                PropertyId)!;
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
                new(),
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

        public TestCompactEndpointConnectionFactory(
            CompactEndpointConnection? connection)
        {
            _connection =
                connection;
        }

        public Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

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