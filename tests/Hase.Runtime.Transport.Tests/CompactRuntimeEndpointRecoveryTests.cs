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

public sealed class CompactRuntimeEndpointRecoveryTests
{
    private static readonly EndpointId EndpointId =
        new("arduino-uno-01");

    private static readonly InstrumentId InstrumentId =
        new("controller-01");

    private static readonly PropertyId PropertyId =
        new("led-state");

    [Fact]
    public async Task RunAsync_ProbeFailure_ShouldPreserveCacheAndRecoverWithReplacement()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            new RuntimeContext().AddEndpoint(
                definition.Materialize(EndpointId));

        var failedProtocolConnection =
            new FirstCompactSerialProtocolConnection();

        var replacementProtocolConnection =
            new ReplacementCompactSerialProtocolConnection();

        var failedConnection =
            new CompactEndpointConnection(
                definition.Materialize(EndpointId),
                failedProtocolConnection);

        var replacementConnection =
            new CompactEndpointConnection(
                definition.Materialize(EndpointId),
                replacementProtocolConnection);

        var connectionFactory =
            new ControlledReplacementConnectionFactory(
                failedConnection,
                replacementConnection);

        CompactPropertyMap propertyMap =
            CreatePropertyMap(definition);

        var coordinator =
            new CompactRuntimeEndpointConnectionCoordinator(
                connectionFactory,
                new SerialTransportOptions("COM10", 115200),
                propertyMap,
                runtimeEndpoint,
                new EndpointDescriptorCompatibilityValidator());

        var supervisor =
            new CompactRuntimeEndpointConnectionSupervisor(
                coordinator,
                propertyMap,
                new ImmediateReconnectPolicy(),
                new CompactEndpointHealthProbeOptions(
                    TimeSpan.FromMilliseconds(10),
                    TimeSpan.FromSeconds(1)));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task supervisionTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        await connectionFactory.WaitForReplacementAttemptAsync();

        RuntimeProperty runtimeProperty =
            runtimeEndpoint
                .FindInstrument(InstrumentId)!
                .FindProperty(PropertyId)!;

        PropertyValue preservedValue =
            runtimeProperty.CurrentValue
            ?? throw new InvalidOperationException(
                "Initial synchronization did not populate the cache.");

        Assert.True(
            Assert.IsType<bool>(preservedValue.Value));

        Assert.Equal(
            1,
            failedProtocolConnection.InvalidateCallCount);

        Assert.Equal(
            1,
            failedProtocolConnection.DisposeCallCount);

        Assert.Same(
            preservedValue,
            runtimeProperty.CurrentValue);

        Assert.Null(
            coordinator.ActiveConnection);

        Assert.Equal(
            2,
            connectionFactory.ConnectCallCount);

        Assert.Equal(
            EndpointConnectionState.Connecting,
            runtimeEndpoint.ConnectionStatus.State);

        connectionFactory.ReleaseReplacement();

        await WaitForReplacementReadyAsync(
            coordinator,
            runtimeEndpoint,
            replacementConnection);

        PropertyValue recoveredValue =
            runtimeProperty.CurrentValue
            ?? throw new InvalidOperationException(
                "Recovery removed the runtime property cache.");

        Assert.False(
            Assert.IsType<bool>(recoveredValue.Value));

        Assert.NotSame(
            preservedValue,
            recoveredValue);

        Assert.True(
            recoveredValue.TimestampUtc
            >= preservedValue.TimestampUtc);

        Assert.Equal(
            PropertyQuality.Good,
            recoveredValue.Quality);

        Assert.Same(
            replacementConnection,
            coordinator.ActiveConnection);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.True(
            replacementProtocolConnection.ExchangeCallCount
            >= 1);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await supervisionTask);

        Assert.Equal(
            1,
            replacementProtocolConnection.DisposeCallCount);

        Assert.Null(
            coordinator.ActiveConnection);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);
    }

    private static async Task WaitForReplacementReadyAsync(
        CompactRuntimeEndpointConnectionCoordinator coordinator,
        RuntimeEndpoint runtimeEndpoint,
        CompactEndpointConnection replacement)
    {
        using var timeoutTokenSource =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(5));

        while (runtimeEndpoint.ConnectionStatus.State
                != EndpointConnectionState.Ready
            || !ReferenceEquals(
                coordinator.ActiveConnection,
                replacement))
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(10),
                timeoutTokenSource.Token);
        }
    }

    private static EndpointDescriptorDefinition CreateDefinition()
    {
        var property =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath("Led", "State"),
                "Built-in LED State",
                new BooleanDataDescriptor())
            {
                AccessMode = PropertyAccessMode.Read
            };

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Arduino Uno GPIO Controller",
                new InstrumentKind("controller"))
            {
                Interface =
                    new InstrumentInterface(
                        properties: [property])
            };

        return new EndpointDescriptorDefinition(
            metadata: new(),
            instruments: [instrument]);
    }

    private static CompactPropertyMap CreatePropertyMap(
        EndpointDescriptorDefinition definition)
    {
        return new CompactPropertyMap(
            definition,
            mappings:
            [
                new CompactPropertyMapping(
                    0x01,
                    InstrumentId,
                    PropertyId,
                    CompactPropertyValueEncoding.Boolean)
            ]);
    }

    private sealed class ImmediateReconnectPolicy
        : IRuntimeEndpointReconnectPolicy
    {
        public TimeSpan GetDelay(int retryAttempt)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(
                retryAttempt);

            return TimeSpan.Zero;
        }
    }

    private sealed class ControlledReplacementConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        private readonly CompactEndpointConnection _firstConnection;
        private readonly CompactEndpointConnection _replacementConnection;

        private readonly TaskCompletionSource _replacementAttempted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _releaseReplacement =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ControlledReplacementConnectionFactory(
            CompactEndpointConnection firstConnection,
            CompactEndpointConnection replacementConnection)
        {
            _firstConnection = firstConnection;
            _replacementConnection = replacementConnection;
        }

        public int ConnectCallCount { get; private set; }

        public Task WaitForReplacementAttemptAsync()
        {
            return _replacementAttempted.Task.WaitAsync(
                TimeSpan.FromSeconds(5));
        }

        public void ReleaseReplacement()
        {
            _releaseReplacement.TrySetResult();
        }

        public async Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConnectCallCount++;

            if (ConnectCallCount == 1)
            {
                return _firstConnection;
            }

            if (ConnectCallCount != 2)
            {
                throw new InvalidOperationException(
                    "Unexpected additional connection attempt.");
            }

            _replacementAttempted.TrySetResult();

            await _releaseReplacement.Task.WaitAsync(
                cancellationToken);

            return _replacementConnection;
        }
    }

    private sealed class FirstCompactSerialProtocolConnection
        : ICompactSerialProtocolConnection
    {
        private int _exchangeCallCount;

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged
        {
            add { }
            remove { }
        }

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

        public int InvalidateCallCount { get; private set; }

        public int DisposeCallCount { get; private set; }

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int call =
                Interlocked.Increment(
                    ref _exchangeCallCount);

            if (call == 2)
            {
                throw new IOException(
                    "The compact serial connection was lost.");
            }

            if (call != 1)
            {
                throw new InvalidOperationException(
                    "Unexpected exchange on failed connection.");
            }

            return Task.FromResult(
                CreateSuccessResponse(request, true));
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

    private sealed class ReplacementCompactSerialProtocolConnection
        : ICompactSerialProtocolConnection
    {
        private int _exchangeCallCount;

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged
        {
            add { }
            remove { }
        }

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

        public int ExchangeCallCount =>
            Volatile.Read(ref _exchangeCallCount);

        public int DisposeCallCount { get; private set; }

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Interlocked.Increment(
                ref _exchangeCallCount);

            return Task.FromResult(
                CreateSuccessResponse(request, false));
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

    private static CompactSerialFrame CreateSuccessResponse(
        CompactSerialFrame request,
        bool value)
    {
        CompactReadPropertyRequest readRequest =
            CompactReadPropertyCodec.DecodeRequest(request);

        return CompactReadPropertyCodec.EncodeResponse(
            new CompactReadPropertyResponse(
                readRequest.CorrelationId,
                readRequest.PropertyId,
                CompactPropertyReadStatus.Success,
                value: new byte[]
                {
                    value ? (byte)0x01 : (byte)0x00
                }));
    }
}