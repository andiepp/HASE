using Hase.CompactProtocol;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactRuntimeEndpointPropertyReadingTests
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
    public async Task ReadPropertyAsync_ReadyEndpoint_ShouldUpdateCache()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var protocolConnection =
            new TestCompactSerialProtocolConnection(
                initialValue: true);

        var coordinator =
            CreateCoordinator(
                definition,
                runtimeEndpoint,
                protocolConnection);

        await coordinator.ConnectAsync();

        protocolConnection.EndpointValue =
            false;

        CompactRuntimePropertySynchronizationResult result =
            await coordinator.ReadPropertyAsync(
                compactPropertyId: 0x01);

        Assert.True(
            result.CacheUpdated);

        Assert.Equal(
            CompactPropertyReadStatus.Success,
            result.Status);

        Assert.False(
            GetCachedValue(
                runtimeEndpoint));

        Assert.Equal(
            2,
            protocolConnection.ReadCallCount);

        await coordinator.DisposeAsync();
    }

    [Fact]
    public async Task ReadPropertyAsync_BeforeConnect_ShouldNotExchange()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        var protocolConnection =
            new TestCompactSerialProtocolConnection(
                initialValue: false);

        var coordinator =
            CreateCoordinator(
                definition,
                CreateRuntimeEndpoint(
                    definition),
                protocolConnection);

        async Task Act()
        {
            _ = await coordinator.ReadPropertyAsync(
                compactPropertyId: 0x01);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            Act);

        Assert.Equal(
            0,
            protocolConnection.ExchangeCallCount);

        await coordinator.DisposeAsync();
    }

    [Fact]
    public async Task ReadPropertyAsync_AfterDispose_ShouldThrow()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        var protocolConnection =
            new TestCompactSerialProtocolConnection(
                initialValue: false);

        var coordinator =
            CreateCoordinator(
                definition,
                CreateRuntimeEndpoint(
                    definition),
                protocolConnection);

        await coordinator.ConnectAsync();
        await coordinator.DisposeAsync();

        int previousExchangeCallCount =
            protocolConnection.ExchangeCallCount;

        async Task Act()
        {
            _ = await coordinator.ReadPropertyAsync(
                compactPropertyId: 0x01);
        }

        await Assert.ThrowsAsync<ObjectDisposedException>(
            Act);

        Assert.Equal(
            previousExchangeCallCount,
            protocolConnection.ExchangeCallCount);
    }

    private static CompactRuntimeEndpointConnectionCoordinator
        CreateCoordinator(
            EndpointDescriptorDefinition definition,
            RuntimeEndpoint runtimeEndpoint,
            ICompactSerialProtocolConnection protocolConnection)
    {
        return new CompactRuntimeEndpointConnectionCoordinator(
            new TestCompactEndpointConnectionFactory(
                new CompactEndpointConnection(
                    definition.Materialize(
                        EndpointId),
                    protocolConnection)),
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

    private static bool GetCachedValue(
        RuntimeEndpoint runtimeEndpoint)
    {
        RuntimeProperty runtimeProperty =
            runtimeEndpoint
                .FindInstrument(
                    InstrumentId)!
                .FindProperty(
                    PropertyId)!;

        Assert.NotNull(
            runtimeProperty.CurrentValue);

        return Assert.IsType<bool>(
            runtimeProperty.CurrentValue.Value);
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
                    PropertyAccessMode.ReadWrite
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
        public TestCompactSerialProtocolConnection(
            bool initialValue)
        {
            EndpointValue =
                initialValue;
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

        public bool EndpointValue
        {
            get;
            set;
        }

        public int ExchangeCallCount
        {
            get;
            private set;
        }

        public int ReadCallCount
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

            ReadCallCount++;

            return Task.FromResult(
                CompactReadPropertyCodec.EncodeResponse(
                    new CompactReadPropertyResponse(
                        readRequest.CorrelationId,
                        readRequest.PropertyId,
                        CompactPropertyReadStatus.Success,
                        new byte[]
                        {
                            EndpointValue
                                ? (byte)1
                                : (byte)0
                        })));
        }

        public void Invalidate()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}