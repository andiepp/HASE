using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Protocol;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionSupervisorIntegrationTests
{
    [Fact]
    public async Task AutomaticReconnect_ShouldResynchronizeDescriptorAndProperty()
    {
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint physicalEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        var dispatcher =
            new RuntimeProtocolDispatcher(
                physicalEndpoint);

        PropertyValue currentPhysicalValue =
            CreatePropertyValue(
                21.5,
                1_750_000_000_000);

        int descriptorRequestCount =
            0;

        int propertyRequestCount =
            0;

        Task<byte[]> ExchangeAsync(
            byte[] requestFrame,
            CancellationToken cancellationToken)
        {
            return HandleProtocolExchangeAsync(
                dispatcher,
                requestFrame,
                currentPhysicalValue,
                () => descriptorRequestCount++,
                () => propertyRequestCount++,
                cancellationToken);
        }

        var initialConnection =
            new TestTransportConnection(
                ExchangeAsync);

        var replacementConnection =
            new TestTransportConnection(
                ExchangeAsync);

        var factory =
            new TestTransportFactory(
                initialConnection,
                replacementConnection);

        await using var connectionManager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint clientEndpoint =
            CreateRuntimeEndpoint(
                CreateDescriptor());

        RuntimeProperty clientProperty =
            GetRuntimeProperty(
                clientEndpoint);

        var readyObserver =
            new ReadyObserver(
                clientEndpoint);

        clientEndpoint.SubscribeConnectionStatus(
            readyObserver);

        var synchronizer =
            new ProtocolRuntimeEndpointSynchronizer(
                new EndpointDescriptorCompatibilityValidator());

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                clientEndpoint,
                synchronizer);

        var supervisor =
            new RuntimeEndpointConnectionSupervisor(
                coordinator,
                new ImmediateReconnectPolicy());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task supervisionTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        await readyObserver.FirstReady;

        Assert.Equal(
            EndpointConnectionState.Ready,
            clientEndpoint.ConnectionStatus.State);

        AssertPropertyValueEquivalent(
            currentPhysicalValue,
            clientProperty.CurrentValue);

        Assert.Equal(
            1,
            descriptorRequestCount);

        Assert.Equal(
            1,
            propertyRequestCount);

        PropertyValue initialCachedValue =
            clientProperty.CurrentValue
            ?? throw new InvalidOperationException(
                "The initial property value was not cached.");

        currentPhysicalValue =
            CreatePropertyValue(
                24.75,
                1_750_000_100_000);

        initialConnection.TransitionTo(
            TransportConnectionState.Faulted);

        AssertPropertyValueEquivalent(
            initialCachedValue,
            clientProperty.CurrentValue);

        await factory.ReplacementConnectStarted;

        Assert.Equal(
            EndpointConnectionState.Reconnecting,
            clientEndpoint.ConnectionStatus.State);

        AssertPropertyValueEquivalent(
            initialCachedValue,
            clientProperty.CurrentValue);

        factory.CompleteReplacementConnection();

        await readyObserver.SecondReady;

        Assert.Equal(
            EndpointConnectionState.Ready,
            clientEndpoint.ConnectionStatus.State);

        Assert.Same(
            replacementConnection,
            connectionManager.CurrentConnection);

        Assert.Equal(
            2,
            factory.ConnectCallCount);

        Assert.Equal(
            1,
            connectionManager.ReplacementCount);

        Assert.Equal(
            2,
            descriptorRequestCount);

        Assert.Equal(
            2,
            propertyRequestCount);

        AssertPropertyValueEquivalent(
            currentPhysicalValue,
            clientProperty.CurrentValue);

        Assert.NotEqual(
            initialCachedValue.Value,
            clientProperty.CurrentValue!.Value);

        Assert.False(
            supervisionTask.IsCompleted);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await supervisionTask);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            clientEndpoint.ConnectionStatus.State);

        AssertPropertyValueEquivalent(
            currentPhysicalValue,
            clientProperty.CurrentValue);

        clientEndpoint.UnsubscribeConnectionStatus(
            readyObserver);
    }

    private static async Task<byte[]> HandleProtocolExchangeAsync(
        IRuntimeProtocolDispatcher dispatcher,
        byte[] requestFrame,
        PropertyValue physicalPropertyValue,
        Action descriptorRequestReceived,
        Action propertyRequestReceived,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            dispatcher);

        ArgumentNullException.ThrowIfNull(
            requestFrame);

        ArgumentNullException.ThrowIfNull(
            physicalPropertyValue);

        cancellationToken.ThrowIfCancellationRequested();

        var envelopeByteCodec =
            new ProtocolEnvelopeByteCodec();

        ProtocolEnvelope requestEnvelope =
            envelopeByteCodec.Decode(
                requestFrame);

        var payloadCodec =
            new BinaryProtocolPayloadCodec();

        ProtocolMessage requestMessage =
            payloadCodec.Decode(
                requestEnvelope);

        ProtocolMessage responseMessage;

        switch (requestMessage)
        {
            case ReadEndpointDescriptorRequest request:
                descriptorRequestReceived();

                responseMessage =
                    await dispatcher.DispatchAsync(
                        request,
                        cancellationToken);
                break;

            case ReadPropertyRequest request:
                propertyRequestReceived();

                ValidatePropertyRequest(
                    request);

                responseMessage =
                    new ReadPropertyResponse(
                        request.CorrelationId,
                        ProtocolResult.Success,
                        physicalPropertyValue);
                break;

            default:
                throw new InvalidDataException(
                    $"The integration endpoint does not support "
                    + $"request type '{requestMessage.MessageType}'.");
        }

        ProtocolEnvelope responseEnvelope =
            payloadCodec.Encode(
                responseMessage);

        return envelopeByteCodec.Encode(
            responseEnvelope);
    }

    private static void ValidatePropertyRequest(
        ReadPropertyRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        Assert.Equal(
            InstrumentId,
            request.InstrumentId);

        Assert.Equal(
            TemperaturePropertyId,
            request.PropertyId);

        Assert.False(
            request.CorrelationId.IsNone);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint(
        EndpointDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            descriptor);
    }

    private static EndpointDescriptor CreateDescriptor()
    {
        var temperatureProperty =
            new PropertyDescriptor(
                TemperaturePropertyId,
                new DescriptorPath(
                    "Environment",
                    "Temperature"),
                "Temperature",
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius))
            {
                AccessMode =
                    PropertyAccessMode.Read,
                Description =
                    "Current measured temperature."
            };

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Environment Sensor",
                new InstrumentKind(
                    "environment-sensor"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            temperatureProperty
                        ])
            };

        return new EndpointDescriptor(
            EndpointId,
            [
                instrument
            ])
        {
            Metadata =
                new EndpointMetadata
                {
                    DisplayName =
                        "Automatic Reconnect Integration Endpoint",
                    Description =
                        "Endpoint used to verify automatic reconnect "
                        + "and complete runtime resynchronization."
                }
        };
    }

    private static PropertyValue CreatePropertyValue(
        double value,
        long timestampMilliseconds)
    {
        return new PropertyValue(
            value,
            DateTimeOffset.FromUnixTimeMilliseconds(
                timestampMilliseconds),
            PropertyQuality.Good);
    }

    private static RuntimeProperty GetRuntimeProperty(
        RuntimeEndpoint runtimeEndpoint)
    {
        RuntimeInstrument runtimeInstrument =
            Assert.Single(
                runtimeEndpoint.Instruments);

        return runtimeInstrument.FindProperty(
                   TemperaturePropertyId)
               ?? throw new InvalidOperationException(
                   $"Runtime property "
                   + $"'{TemperaturePropertyId.Value}' was not found.");
    }

    private static void AssertPropertyValueEquivalent(
        PropertyValue expected,
        PropertyValue? actual)
    {
        ArgumentNullException.ThrowIfNull(
            expected);

        Assert.NotNull(
            actual);

        Assert.Equal(
            expected.Value,
            actual!.Value);

        Assert.Equal(
            expected.TimestampUtc,
            actual.TimestampUtc);

        Assert.Equal(
            expected.Quality,
            actual.Quality);
    }

    private static readonly EndpointId EndpointId =
        new(
            "automatic-reconnect-endpoint");

    private static readonly InstrumentId InstrumentId =
        new(
            "environment-sensor-01");

    private static readonly PropertyId TemperaturePropertyId =
        new(
            "environment.temperature");

    private sealed class ImmediateReconnectPolicy
        : IRuntimeEndpointReconnectPolicy
    {
        public TimeSpan GetDelay(
            int retryAttempt)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(
                retryAttempt);

            return TimeSpan.Zero;
        }
    }

    private sealed class TestTransportFactory
        : ITransportFactory
    {
        private readonly ITransportConnection _initialConnection;
        private readonly ITransportConnection _replacementConnection;

        private readonly TaskCompletionSource<bool>
            _replacementConnectStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _completeReplacementConnection =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public TestTransportFactory(
            ITransportConnection initialConnection,
            ITransportConnection replacementConnection)
        {
            _initialConnection =
                initialConnection
                ?? throw new ArgumentNullException(
                    nameof(initialConnection));

            _replacementConnection =
                replacementConnection
                ?? throw new ArgumentNullException(
                    nameof(replacementConnection));
        }

        public int ConnectCallCount
        {
            get;
            private set;
        }

        public Task ReplacementConnectStarted =>
            _replacementConnectStarted.Task;

        public void CompleteReplacementConnection()
        {
            _completeReplacementConnection.TrySetResult(
                true);
        }

        public async Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConnectCallCount++;

            if (ConnectCallCount == 1)
            {
                return _initialConnection;
            }

            if (ConnectCallCount == 2)
            {
                _replacementConnectStarted.TrySetResult(
                    true);

                await _completeReplacementConnection.Task.WaitAsync(
                    cancellationToken);

                return _replacementConnection;
            }

            throw new InvalidOperationException(
                "No additional integration transport connection "
                + "is available.");
        }
    }

    private sealed class ReadyObserver
        : IEndpointConnectionStatusObserver
    {
        private readonly RuntimeEndpoint _endpoint;

        private readonly TaskCompletionSource<bool>
            _firstReady =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _secondReady =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private int _readyCount;

        public ReadyObserver(
            RuntimeEndpoint endpoint)
        {
            _endpoint =
                endpoint
                ?? throw new ArgumentNullException(
                    nameof(endpoint));
        }

        public Task FirstReady =>
            _firstReady.Task;

        public Task SecondReady =>
            _secondReady.Task;

        public void OnEndpointConnectionStatusChanged(
            EndpointConnectionStatusChanged change)
        {
            ArgumentNullException.ThrowIfNull(
                change);

            Assert.Same(
                _endpoint,
                change.Endpoint);

            if (change.CurrentStatus.State
                != EndpointConnectionState.Ready)
            {
                return;
            }

            _readyCount++;

            if (_readyCount == 1)
            {
                _firstReady.TrySetResult(
                    true);
            }
            else if (_readyCount == 2)
            {
                _secondReady.TrySetResult(
                    true);
            }
        }
    }

    private sealed class TestTransportConnection
        : ITransportConnection,
          IAsyncDisposable
    {
        private readonly Func<
            byte[],
            CancellationToken,
            Task<byte[]>> _exchangeHandler;

        private TransportConnectionState _state =
            TransportConnectionState.Connected;

        public TestTransportConnection(
            Func<
                byte[],
                CancellationToken,
                Task<byte[]>> exchangeHandler)
        {
            _exchangeHandler =
                exchangeHandler
                ?? throw new ArgumentNullException(
                    nameof(exchangeHandler));
        }

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State =>
            _state;

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            cancellationToken.ThrowIfCancellationRequested();

            if (_state != TransportConnectionState.Connected)
            {
                throw new InvalidOperationException(
                    "The integration transport connection is not connected.");
            }

            return _exchangeHandler(
                request,
                cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            TransitionTo(
                TransportConnectionState.Closed);

            return ValueTask.CompletedTask;
        }

        public void TransitionTo(
            TransportConnectionState state)
        {
            TransportConnectionState previousState =
                _state;

            if (previousState == state)
            {
                return;
            }

            _state =
                state;

            StateChanged?.Invoke(
                this,
                new TransportConnectionStateChangedEventArgs(
                    previousState,
                    state));
        }
    }
}
