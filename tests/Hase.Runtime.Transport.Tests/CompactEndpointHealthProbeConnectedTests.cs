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

public sealed class CompactEndpointHealthProbeConnectedTests
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
    public async Task ProbeAsync_Success_ShouldRefreshCacheAndRemainReady()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var protocolConnection =
            new TestCompactSerialProtocolConnection(
                [
                    new TestResponse(
                        CompactPropertyReadStatus.Success,
                        Value: new byte[]
                        {
                            0x01
                        }),
                    new TestResponse(
                        CompactPropertyReadStatus.Success,
                        Value: new byte[]
                        {
                            0x00
                        })
                ]);

        CompactRuntimeEndpointConnectionCoordinator coordinator =
            CreateCoordinator(
                definition,
                runtimeEndpoint,
                protocolConnection);

        await coordinator.ConnectAsync();

        RuntimeProperty runtimeProperty =
            FindRuntimeProperty(
                runtimeEndpoint);

        PropertyValue initialValue =
            runtimeProperty.CurrentValue
            ?? throw new InvalidOperationException(
                "Initial synchronization did not populate the cache.");

        Assert.True(
            Assert.IsType<bool>(
                initialValue.Value));

        var probe =
            new CompactEndpointHealthProbe(
                coordinator,
                CreatePropertyMap(
                    definition),
                CompactEndpointHealthProbeOptions.Default);

        await probe.ProbeAsync();

        PropertyValue updatedValue =
            runtimeProperty.CurrentValue
            ?? throw new InvalidOperationException(
                "The health probe cleared the cache.");

        Assert.False(
            Assert.IsType<bool>(
                updatedValue.Value));

        Assert.NotSame(
            initialValue,
            updatedValue);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            2,
            protocolConnection.ExchangeCallCount);

        Assert.Equal(
            0,
            protocolConnection.InvalidateCallCount);

        await coordinator.DisposeAsync();
    }

    [Fact]
    public async Task ProbeAsync_ReadFailed_ShouldPreserveCacheInvalidateAndFault()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var protocolConnection =
            new TestCompactSerialProtocolConnection(
                [
                    new TestResponse(
                        CompactPropertyReadStatus.Success,
                        Value: new byte[]
                        {
                            0x01
                        }),
                    new TestResponse(
                        CompactPropertyReadStatus.ReadFailed,
                        Value: ReadOnlyMemory<byte>.Empty)
                ]);

        CompactRuntimeEndpointConnectionCoordinator coordinator =
            CreateCoordinator(
                definition,
                runtimeEndpoint,
                protocolConnection);

        await coordinator.ConnectAsync();

        RuntimeProperty runtimeProperty =
            FindRuntimeProperty(
                runtimeEndpoint);

        PropertyValue initialValue =
            runtimeProperty.CurrentValue
            ?? throw new InvalidOperationException(
                "Initial synchronization did not populate the cache.");

        var probe =
            new CompactEndpointHealthProbe(
                coordinator,
                CreatePropertyMap(
                    definition),
                CompactEndpointHealthProbeOptions.Default);

        async Task Act()
        {
            await probe.ProbeAsync();
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);

        Assert.Same(
            initialValue,
            runtimeProperty.CurrentValue);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            1,
            protocolConnection.InvalidateCallCount);

        Assert.Equal(
            2,
            protocolConnection.ExchangeCallCount);

        await coordinator.DisposeAsync();
    }

    private static CompactRuntimeEndpointConnectionCoordinator
        CreateCoordinator(
            EndpointDescriptorDefinition definition,
            RuntimeEndpoint runtimeEndpoint,
            ICompactSerialProtocolConnection protocolConnection)
    {
        var factory =
            new TestCompactEndpointConnectionFactory(
                new CompactEndpointConnection(
                    definition.Materialize(
                        EndpointId),
                    protocolConnection));

        return new CompactRuntimeEndpointConnectionCoordinator(
            factory,
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

    private sealed record TestResponse(
        CompactPropertyReadStatus Status,
        ReadOnlyMemory<byte> Value);

    private sealed class TestCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        private readonly CompactEndpointConnection _connection;

        public TestCompactEndpointConnectionFactory(
            CompactEndpointConnection connection)
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
                _connection);
        }
    }

    private sealed class TestCompactSerialProtocolConnection
        : ICompactSerialProtocolConnection
    {
        private readonly Queue<TestResponse> _responses;

        public TestCompactSerialProtocolConnection(
            IEnumerable<TestResponse> responses)
        {
            _responses =
                new Queue<TestResponse>(
                    responses);
        }

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

        public int InvalidateCallCount
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

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    "No compact test response remains.");
            }

            CompactReadPropertyRequest readRequest =
                CompactReadPropertyCodec.DecodeRequest(
                    request);

            TestResponse response =
                _responses.Dequeue();

            return Task.FromResult(
                CompactReadPropertyCodec.EncodeResponse(
                    new CompactReadPropertyResponse(
                        readRequest.CorrelationId,
                        readRequest.PropertyId,
                        response.Status,
                        response.Value)));
        }

        public void Invalidate()
        {
            InvalidateCallCount++;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}