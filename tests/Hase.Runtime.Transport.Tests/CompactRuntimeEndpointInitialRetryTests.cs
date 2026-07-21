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

public sealed class CompactRuntimeEndpointInitialRetryTests
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
    public async Task RunAsync_InitialFailure_ShouldRetryImmediatelyAndBecomeReady()
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

        var replacement =
            new CompactEndpointConnection(
                definition.Materialize(
                    EndpointId),
                protocolConnection);

        var connectionFactory =
            new TestCompactEndpointConnectionFactory(
                replacement);

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

        var reconnectPolicy =
            new RecordingReconnectPolicy();

        var supervisor =
            new CompactRuntimeEndpointConnectionSupervisor(
                coordinator,
                propertyMap,
                reconnectPolicy,
                new CompactEndpointHealthProbeOptions(
                    probeInterval:
                        TimeSpan.FromMinutes(
                            1),
                    probeTimeout:
                        TimeSpan.FromSeconds(
                            3)));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task supervisionTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        await WaitForStateAsync(
            runtimeEndpoint,
            EndpointConnectionState.Ready);

        Assert.Equal(
            2,
            connectionFactory.ConnectCallCount);

        int retryAttempt =
            Assert.Single(
                reconnectPolicy.Attempts);

        Assert.Equal(
            0,
            retryAttempt);

        Assert.Equal(
            1,
            protocolConnection.ExchangeCallCount);

        Assert.Same(
            replacement,
            coordinator.ActiveConnection);

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

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await supervisionTask);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
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

    private sealed class RecordingReconnectPolicy
        : IRuntimeEndpointReconnectPolicy
    {
        public List<int> Attempts
        {
            get;
        } =
            [];

        public TimeSpan GetDelay(
            int retryAttempt)
        {
            Attempts.Add(
                retryAttempt);

            return TimeSpan.Zero;
        }
    }

    private sealed class TestCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        private readonly CompactEndpointConnection _replacement;

        public TestCompactEndpointConnectionFactory(
            CompactEndpointConnection replacement)
        {
            _replacement =
                replacement;
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

            if (ConnectCallCount == 1)
            {
                return Task.FromException<
                    CompactEndpointConnection>(
                        new IOException(
                            "The configured serial port is unavailable."));
            }

            if (ConnectCallCount == 2)
            {
                return Task.FromResult(
                    _replacement);
            }

            throw new InvalidOperationException(
                "The supervisor attempted an unexpected additional "
                + "connection.");
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