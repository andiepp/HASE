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

public sealed class CompactEndpointHealthProbeCancellationTests
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
    public async Task ProbeAsync_Timeout_ShouldInvalidateAndFault()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var protocolConnection =
            new BlockingCompactSerialProtocolConnection();

        CompactPropertyMap propertyMap =
            CreatePropertyMap(
                definition);

        CompactRuntimeEndpointConnectionCoordinator coordinator =
            CreateCoordinator(
                definition,
                propertyMap,
                runtimeEndpoint,
                protocolConnection);

        await coordinator.ConnectAsync();

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        RuntimeProperty runtimeProperty =
            FindRuntimeProperty(
                runtimeEndpoint);

        PropertyValue cachedValue =
            runtimeProperty.CurrentValue
            ?? throw new InvalidOperationException(
                "Initial synchronization did not populate the cache.");

        var probe =
            new CompactEndpointHealthProbe(
                coordinator,
                propertyMap,
                new CompactEndpointHealthProbeOptions(
                    probeInterval:
                        TimeSpan.FromSeconds(
                            1),
                    probeTimeout:
                        TimeSpan.FromMilliseconds(
                            25)));

        async Task Act()
        {
            await probe.ProbeAsync();
        }

        await Assert.ThrowsAsync<TimeoutException>(
            Act);

        Assert.Equal(
            2,
            protocolConnection.ExchangeCallCount);

        Assert.Equal(
            1,
            protocolConnection.InvalidateCallCount);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Same(
            cachedValue,
            runtimeProperty.CurrentValue);

        Assert.NotNull(
            coordinator.ActiveConnection);

        await coordinator.DisposeAsync();

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    [Fact]
    public async Task ProbeAsync_CallerCancellation_ShouldRemainReadyWithoutInvalidation()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var protocolConnection =
            new BlockingCompactSerialProtocolConnection();

        CompactPropertyMap propertyMap =
            CreatePropertyMap(
                definition);

        CompactRuntimeEndpointConnectionCoordinator coordinator =
            CreateCoordinator(
                definition,
                propertyMap,
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

        var probe =
            new CompactEndpointHealthProbe(
                coordinator,
                propertyMap,
                new CompactEndpointHealthProbeOptions(
                    probeInterval:
                        TimeSpan.FromSeconds(
                            1),
                    probeTimeout:
                        TimeSpan.FromSeconds(
                            5)));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task probeTask =
            probe.ProbeAsync(
                cancellationTokenSource.Token);

        await protocolConnection.WaitForBlockedExchangeAsync();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await probeTask);

        Assert.Equal(
            2,
            protocolConnection.ExchangeCallCount);

        Assert.Equal(
            0,
            protocolConnection.InvalidateCallCount);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Same(
            cachedValue,
            runtimeProperty.CurrentValue);

        Assert.NotNull(
            coordinator.ActiveConnection);

        await coordinator.DisposeAsync();

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    private static CompactRuntimeEndpointConnectionCoordinator
        CreateCoordinator(
            EndpointDescriptorDefinition definition,
            CompactPropertyMap propertyMap,
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
            propertyMap,
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

    private sealed class BlockingCompactSerialProtocolConnection
        : ICompactSerialProtocolConnection
    {
        private readonly TaskCompletionSource _blockedExchangeStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

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

        public Task WaitForBlockedExchangeAsync()
        {
            return _blockedExchangeStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(
                    5));
        }

        public async Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int call =
                Interlocked.Increment(
                    ref _exchangeCallCount);

            if (call == 1)
            {
                return CreateSuccessfulResponse(
                    request);
            }

            if (call != 2)
            {
                throw new InvalidOperationException(
                    "The test connection received an unexpected additional "
                    + "exchange.");
            }

            _blockedExchangeStarted.TrySetResult();

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            throw new InvalidOperationException(
                "The blocked exchange completed without cancellation.");
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

        private static CompactSerialFrame CreateSuccessfulResponse(
            CompactSerialFrame request)
        {
            CompactReadPropertyRequest readRequest =
                CompactReadPropertyCodec.DecodeRequest(
                    request);

            return CompactReadPropertyCodec.EncodeResponse(
                new CompactReadPropertyResponse(
                    readRequest.CorrelationId,
                    readRequest.PropertyId,
                    CompactPropertyReadStatus.Success,
                    value: new byte[]
                    {
                        0x01
                    }));
        }
    }
}