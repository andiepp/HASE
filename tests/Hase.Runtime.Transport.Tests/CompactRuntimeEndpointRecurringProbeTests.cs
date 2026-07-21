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

public sealed class CompactRuntimeEndpointRecurringProbeTests
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
    public async Task RunAsync_SuccessfulProbes_ShouldRepeatAndRemainReady()
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
                        TimeSpan.FromMilliseconds(
                            10),
                    probeTimeout:
                        TimeSpan.FromSeconds(
                            1)));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task supervisionTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        await WaitForExchangeCountAsync(
            protocolConnection,
            expectedMinimum:
                3);

        Assert.Equal(
            1,
            connectionFactory.ConnectCallCount);

        Assert.True(
            protocolConnection.ExchangeCallCount
            >= 3);

        Assert.Equal(
            0,
            protocolConnection.InvalidateCallCount);

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

        Assert.IsType<bool>(
            runtimeProperty.CurrentValue.Value);

        Assert.Equal(
            PropertyQuality.Good,
            runtimeProperty.CurrentValue.Quality);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await supervisionTask);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Null(
            coordinator.ActiveConnection);

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    private static async Task WaitForExchangeCountAsync(
        TestCompactSerialProtocolConnection connection,
        int expectedMinimum)
    {
        using var timeoutTokenSource =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(
                    5));

        while (connection.ExchangeCallCount
            < expectedMinimum)
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
        private readonly CompactEndpointConnection _connection;

        public TestCompactEndpointConnectionFactory(
            CompactEndpointConnection connection)
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

            if (ConnectCallCount != 1)
            {
                throw new InvalidOperationException(
                    "Successful recurring probes must not open another "
                    + "physical connection.");
            }

            return Task.FromResult(
                _connection);
        }
    }

    private sealed class TestCompactSerialProtocolConnection
        : ICompactSerialProtocolConnection
    {
        private int _exchangeCallCount;

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

        public int ExchangeCallCount =>
            Volatile.Read(
                ref _exchangeCallCount);

        public int InvalidateCallCount
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

            Interlocked.Increment(
                ref _exchangeCallCount);

            CompactReadPropertyRequest readRequest =
                CompactReadPropertyCodec.DecodeRequest(
                    request);

            bool state =
                ExchangeCallCount % 2 != 0;

            return Task.FromResult(
                CompactReadPropertyCodec.EncodeResponse(
                    new CompactReadPropertyResponse(
                        readRequest.CorrelationId,
                        readRequest.PropertyId,
                        CompactPropertyReadStatus.Success,
                        value: new byte[]
                        {
                            state
                                ? (byte)0x01
                                : (byte)0x00
                        })));
        }

        public void Invalidate()
        {
            InvalidateCallCount++;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            return ValueTask.CompletedTask;
        }
    }
}