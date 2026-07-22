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

public sealed class CompactRuntimeEndpointPropertyWriteDisposalTests
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
    public async Task DisposeAsync_InProgressWrite_ShouldWaitAndThenDisconnect()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            new RuntimeContext()
                .AddEndpoint(
                    definition.Materialize(
                        EndpointId));

        var protocolConnection =
            new BlockingConfirmationProtocolConnection();

        var coordinator =
            new CompactRuntimeEndpointConnectionCoordinator(
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

        await coordinator.ConnectAsync();

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Task<CompactRuntimePropertyWriteResult> writeTask =
            coordinator.WritePropertyAsync(
                compactPropertyId: 0x01,
                value: true);

        await protocolConnection.ConfirmationReadStarted;

        Task disposeTask =
            coordinator
                .DisposeAsync()
                .AsTask();

        await Task.Yield();

        Assert.False(
            disposeTask.IsCompleted);

        Assert.Equal(
            0,
            protocolConnection.DisposeCallCount);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        protocolConnection.ReleaseConfirmationRead();

        CompactRuntimePropertyWriteResult result =
            await writeTask;

        Assert.True(
            result.CacheUpdated);

        Assert.Equal(
            CompactPropertyWriteStatus.Success,
            result.WriteStatus);

        Assert.Equal(
            CompactPropertyReadStatus.Success,
            result.ConfirmationReadStatus);

        await disposeTask;

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);

        Assert.Null(
            coordinator.ActiveConnection);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
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
    }

    [Fact]
    public async Task WritePropertyAsync_QueuedBehindDisposal_ShouldBeRejected()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            new RuntimeContext()
                .AddEndpoint(
                    definition.Materialize(
                        EndpointId));

        var protocolConnection =
            new BlockingConfirmationProtocolConnection();

        var coordinator =
            new CompactRuntimeEndpointConnectionCoordinator(
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

        await coordinator.ConnectAsync();

        Task<CompactRuntimePropertyWriteResult> activeWriteTask =
            coordinator.WritePropertyAsync(
                compactPropertyId: 0x01,
                value: true);

        await protocolConnection.ConfirmationReadStarted;

        Task disposeTask =
            coordinator
                .DisposeAsync()
                .AsTask();

        Task<CompactRuntimePropertyWriteResult> queuedWriteTask =
            coordinator.WritePropertyAsync(
                compactPropertyId: 0x01,
                value: false);

        Assert.False(
            disposeTask.IsCompleted);

        Assert.False(
            queuedWriteTask.IsCompleted);

        protocolConnection.ReleaseConfirmationRead();

        CompactRuntimePropertyWriteResult activeResult =
            await activeWriteTask;

        Assert.True(
            activeResult.CacheUpdated);

        await disposeTask;

        async Task Act()
        {
            _ = await queuedWriteTask;
        }

        await Assert.ThrowsAsync<ObjectDisposedException>(
            Act);

        Assert.Equal(
            1,
            protocolConnection.WriteCallCount);

        Assert.Equal(
            2,
            protocolConnection.ReadCallCount);

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);
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

    private sealed class BlockingConfirmationProtocolConnection
        : ICompactSerialProtocolConnection
    {
        private readonly TaskCompletionSource
            _confirmationReadStarted =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource
            _confirmationReadRelease =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private bool _endpointValue;

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

        public Task ConfirmationReadStarted =>
            _confirmationReadStarted.Task;

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

        public int DisposeCallCount
        {
            get;
            private set;
        }

        public void ReleaseConfirmationRead()
        {
            _confirmationReadRelease.TrySetResult();
        }

        public async Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request.MessageType
                == (byte)CompactSerialMessageType.WritePropertyRequest)
            {
                CompactWritePropertyRequest writeRequest =
                    CompactWritePropertyCodec.DecodeRequest(
                        request);

                WriteCallCount++;

                _endpointValue =
                    writeRequest.Value.Span[0]
                    == 0x01;

                return CompactWritePropertyCodec.EncodeResponse(
                    new CompactWritePropertyResponse(
                        writeRequest.CorrelationId,
                        writeRequest.PropertyId,
                        CompactPropertyWriteStatus.Success));
            }

            CompactReadPropertyRequest readRequest =
                CompactReadPropertyCodec.DecodeRequest(
                    request);

            ReadCallCount++;

            if (ReadCallCount == 2)
            {
                _confirmationReadStarted.TrySetResult();

                await _confirmationReadRelease.Task.WaitAsync(
                    cancellationToken);
            }

            return CompactReadPropertyCodec.EncodeResponse(
                new CompactReadPropertyResponse(
                    readRequest.CorrelationId,
                    readRequest.PropertyId,
                    CompactPropertyReadStatus.Success,
                    value: new byte[]
                    {
                        _endpointValue
                            ? (byte)0x01
                            : (byte)0x00
                    }));
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