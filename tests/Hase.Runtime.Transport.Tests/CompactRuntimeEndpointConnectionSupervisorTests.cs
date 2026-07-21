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

public sealed class CompactRuntimeEndpointConnectionSupervisorTests
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
    public async Task RunAsync_RepeatedCalls_ShouldReturnSameTaskAndCancelDisconnected()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            new RuntimeContext()
                .AddEndpoint(
                    definition.Materialize(
                        EndpointId));

        var protocolConnection =
            new TestCompactSerialProtocolConnection();

        var connectionFactory =
            new TestCompactEndpointConnectionFactory(
                new CompactEndpointConnection(
                    definition.Materialize(
                        EndpointId),
                    protocolConnection));

        CompactPropertyMap propertyMap =
            CreatePropertyMap(
                definition);

        var coordinator =
            new CompactRuntimeEndpointConnectionCoordinator(
                connectionFactory,
                new SerialTransportOptions(
                    "COM10",
                    115200),
                propertyMap,
                runtimeEndpoint,
                new EndpointDescriptorCompatibilityValidator());

        var supervisor =
            new CompactRuntimeEndpointConnectionSupervisor(
                coordinator,
                propertyMap,
                new DefaultRuntimeEndpointReconnectPolicy(),
                new CompactEndpointHealthProbeOptions(
                    probeInterval:
                        TimeSpan.FromMinutes(
                            1),
                    probeTimeout:
                        TimeSpan.FromSeconds(
                            3)));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task firstTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        Task secondTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        Assert.Same(
            firstTask,
            secondTask);

        await WaitForStateAsync(
            runtimeEndpoint,
            EndpointConnectionState.Ready);

        Assert.Equal(
            1,
            connectionFactory.ConnectCallCount);

        Assert.Equal(
            1,
            protocolConnection.ExchangeCallCount);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await firstTask);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Null(
            coordinator.ActiveConnection);

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    [Fact]
    public void Constructor_NullCoordinator_ShouldThrow()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        void Act()
        {
            _ = new CompactRuntimeEndpointConnectionSupervisor(
                null!,
                CreatePropertyMap(
                    definition),
                new DefaultRuntimeEndpointReconnectPolicy(),
                CompactEndpointHealthProbeOptions.Default);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullPropertyMap_ShouldThrow()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        CompactRuntimeEndpointConnectionCoordinator coordinator =
            CreateCoordinator(
                definition);

        void Act()
        {
            _ = new CompactRuntimeEndpointConnectionSupervisor(
                coordinator,
                null!,
                new DefaultRuntimeEndpointReconnectPolicy(),
                CompactEndpointHealthProbeOptions.Default);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullReconnectPolicy_ShouldThrow()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        CompactRuntimeEndpointConnectionCoordinator coordinator =
            CreateCoordinator(
                definition);

        void Act()
        {
            _ = new CompactRuntimeEndpointConnectionSupervisor(
                coordinator,
                CreatePropertyMap(
                    definition),
                null!,
                CompactEndpointHealthProbeOptions.Default);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullProbeOptions_ShouldThrow()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        CompactRuntimeEndpointConnectionCoordinator coordinator =
            CreateCoordinator(
                definition);

        void Act()
        {
            _ = new CompactRuntimeEndpointConnectionSupervisor(
                coordinator,
                CreatePropertyMap(
                    definition),
                new DefaultRuntimeEndpointReconnectPolicy(),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static CompactRuntimeEndpointConnectionCoordinator
        CreateCoordinator(
            EndpointDescriptorDefinition definition)
    {
        RuntimeEndpoint runtimeEndpoint =
            new RuntimeContext()
                .AddEndpoint(
                    definition.Materialize(
                        EndpointId));

        return new CompactRuntimeEndpointConnectionCoordinator(
            new TestCompactEndpointConnectionFactory(
                connection: null),
            new SerialTransportOptions(
                "COM10",
                115200),
            CreatePropertyMap(
                definition),
            runtimeEndpoint,
            new EndpointDescriptorCompatibilityValidator());
    }

    private static async Task WaitForStateAsync(
        RuntimeEndpoint runtimeEndpoint,
        EndpointConnectionState expectedState)
    {
        using var timeoutTokenSource =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(
                    5));

        while (runtimeEndpoint.ConnectionStatus.State
            != expectedState)
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(
                    10),
                timeoutTokenSource.Token);
        }
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

        public int ConnectCallCount
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