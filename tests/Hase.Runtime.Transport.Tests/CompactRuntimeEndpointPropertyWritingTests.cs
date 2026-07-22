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

public sealed class CompactRuntimeEndpointPropertyWritingTests
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
    public async Task WritePropertyAsync_ReadyEndpoint_ShouldConfirmAndUpdateCache()
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

        Assert.True(
            GetCachedValue(
                runtimeEndpoint));

        CompactRuntimePropertyWriteResult result =
            await coordinator.WritePropertyAsync(
                compactPropertyId: 0x01,
                value: false);

        Assert.True(
            result.CacheUpdated);

        Assert.Equal(
            CompactPropertyWriteStatus.Success,
            result.WriteStatus);

        Assert.Equal(
            CompactPropertyReadStatus.Success,
            result.ConfirmationReadStatus);

        Assert.False(
            GetCachedValue(
                runtimeEndpoint));

        Assert.False(
            protocolConnection.EndpointValue);

        Assert.Equal(
            1,
            protocolConnection.WriteCallCount);

        Assert.Equal(
            2,
            protocolConnection.ReadCallCount);

        await coordinator.DisposeAsync();
    }

    [Fact]
    public async Task WritePropertyAsync_BeforeConnect_ShouldNotExchange()
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
            _ = await coordinator.WritePropertyAsync(
                compactPropertyId: 0x01,
                value: true);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            Act);

        Assert.Equal(
            0,
            protocolConnection.ExchangeCallCount);

        await coordinator.DisposeAsync();
    }

    [Fact]
    public async Task WritePropertyAsync_AfterDispose_ShouldThrow()
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
            _ = await coordinator.WritePropertyAsync(
                compactPropertyId: 0x01,
                value: true);
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
            private set;
        }

        public int ExchangeCallCount =>
            ReadCallCount
            + WriteCallCount;

        public int ReadCallCount
        {
            get;
            private set;
        }

        public int WriteCallCount
        {
            get;
            private set;
        }

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request.MessageType
                == (byte)CompactSerialMessageType.ReadPropertyRequest)
            {
                ReadCallCount++;

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
                                EndpointValue
                                    ? (byte)0x01
                                    : (byte)0x00
                            })));
            }

            CompactWritePropertyRequest writeRequest =
                CompactWritePropertyCodec.DecodeRequest(
                    request);

            WriteCallCount++;

            EndpointValue =
                writeRequest.Value.Span[0]
                == 0x01;

            return Task.FromResult(
                CompactWritePropertyCodec.EncodeResponse(
                    new CompactWritePropertyResponse(
                        writeRequest.CorrelationId,
                        writeRequest.PropertyId,
                        CompactPropertyWriteStatus.Success)));
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