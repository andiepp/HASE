using System.Threading.Channels;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class
    RuntimeEndpointConnectionCoordinatorEventRouterMigrationTests
{
    private static readonly InstrumentId ControllerInstrumentId =
        new(
            "controller-01");

    private static readonly DescriptorPath ButtonPressedPath =
        new(
            "Controller",
            "ButtonPressed");

    [Fact]
    public async Task ReconnectAsync_ReplacementDuplexSession_ShouldContinueRoutingRuntimeEvents()
    {
        // Arrange
        var initialConnection =
            new TestDuplexTransportConnection();

        var replacementConnection =
            new TestDuplexTransportConnection();

        var transportFactory =
            new QueuedTransportFactory(
                initialConnection,
                replacementConnection);

        await using var connectionManager =
            new TransportConnectionManager(
                transportFactory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        RuntimeEvent runtimeEvent =
            GetButtonPressedRuntimeEvent(
                runtimeEndpoint);

        var eventObserver =
            new RecordingRuntimeEventObserver();

        runtimeEvent.Subscribe(
            eventObserver);

        var synchronizer =
            new TestProtocolSynchronizer();

        EventNotification initialNotification =
            CreateNotification(
                new DateTimeOffset(
                    2026,
                    7,
                    18,
                    13,
                    0,
                    0,
                    TimeSpan.Zero));

        EventNotification replacementNotification =
            CreateNotification(
                new DateTimeOffset(
                    2026,
                    7,
                    18,
                    13,
                    1,
                    0,
                    TimeSpan.Zero));

        await using (
            var coordinator =
                new RuntimeEndpointConnectionCoordinator(
                    connectionManager,
                    runtimeEndpoint,
                    synchronizer))
        {
            await coordinator.ConnectAsync();

            await initialConnection.ReceiveStarted;

            initialConnection.QueueReceivedMessage(
                initialNotification);

            RuntimeEventOccurrence initialOccurrence =
                await eventObserver.FirstOccurrence;

            Assert.Same(
                runtimeEvent,
                initialOccurrence.Event);

            Assert.Equal(
                initialNotification.TimestampUtc,
                initialOccurrence.TimestampUtc);

            Assert.Null(
                initialOccurrence.Value);

            Assert.Equal(
                1,
                eventObserver.OccurrenceCount);

            initialConnection.Fault(
                new IOException(
                    "The simulated initial duplex session failed."));

            await initialConnection.ReceiveStopped;

            Assert.Equal(
                EndpointConnectionState.Faulted,
                runtimeEndpoint.ConnectionStatus.State);

            await coordinator.ReconnectAsync();

            await replacementConnection.ReceiveStarted;

            Assert.Equal(
                EndpointConnectionState.Ready,
                runtimeEndpoint.ConnectionStatus.State);

            replacementConnection.QueueReceivedMessage(
                replacementNotification);

            RuntimeEventOccurrence replacementOccurrence =
                await eventObserver.SecondOccurrence;

            Assert.Same(
                runtimeEvent,
                replacementOccurrence.Event);

            Assert.Equal(
                replacementNotification.TimestampUtc,
                replacementOccurrence.TimestampUtc);

            Assert.Null(
                replacementOccurrence.Value);

            Assert.Equal(
                2,
                eventObserver.OccurrenceCount);

            Assert.Equal(
                2,
                synchronizer.SynchronizationCount);

            Assert.Equal(
                2,
                transportFactory.ConnectCallCount);

            Assert.Equal(
                1,
                connectionManager.ReplacementCount);
        }

        runtimeEvent.Unsubscribe(
            eventObserver);

        await replacementConnection.ReceiveStopped;

        Assert.True(
            replacementConnection.ReceivedCancellationToken
                .IsCancellationRequested);
    }

    private static EventNotification CreateNotification(
        DateTimeOffset timestampUtc)
    {
        return new EventNotification(
            ControllerInstrumentId,
            ButtonPressedPath,
            timestampUtc,
            null);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var buttonPressedEventDescriptor =
            new EventDescriptor(
                ButtonPressedPath,
                "Button Pressed")
            {
                Description =
                    "Raised when the physical GPIO17 pushbutton is pressed."
            };

        var controllerInstrumentDescriptor =
            new InstrumentDescriptor(
                ControllerInstrumentId,
                "ESP32 GPIO Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        events:
                        [
                            buttonPressedEventDescriptor
                        ])
            };

        var endpointDescriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "coordinator-event-router-migration-endpoint"),
                [
                    controllerInstrumentDescriptor
                ])
            {
                Metadata =
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Coordinator Event Router Migration Endpoint",
                        Description =
                            "Endpoint used to verify runtime event routing "
                            + "after duplex session replacement."
                    }
            };

        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            endpointDescriptor);
    }

    private static RuntimeEvent GetButtonPressedRuntimeEvent(
        RuntimeEndpoint runtimeEndpoint)
    {
        RuntimeInstrument runtimeInstrument =
            runtimeEndpoint.FindInstrument(
                ControllerInstrumentId)
            ?? throw new InvalidOperationException(
                "The controller runtime instrument was not found.");

        return runtimeInstrument.FindEvent(
            ButtonPressedPath)
            ?? throw new InvalidOperationException(
                "The button-pressed runtime event was not found.");
    }

    private sealed class TestProtocolSynchronizer
        : IRuntimeEndpointSynchronizer,
          IRuntimeProtocolEndpointSynchronizer
    {
        public int SynchronizationCount
        {
            get;
            private set;
        }

        public Task SynchronizeAsync(
            ITransportConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The transport synchronization contract should not "
                + "be selected.");
        }

        public Task SynchronizeAsync(
            IRuntimeProtocolConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                connection);

            ArgumentNullException.ThrowIfNull(
                runtimeEndpoint);

            cancellationToken.ThrowIfCancellationRequested();

            SynchronizationCount++;

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRuntimeEventObserver
        : IRuntimeEventObserver
    {
        private readonly TaskCompletionSource<RuntimeEventOccurrence>
            _firstOccurrence =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<RuntimeEventOccurrence>
            _secondOccurrence =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private int _occurrenceCount;

        public Task<RuntimeEventOccurrence> FirstOccurrence =>
            _firstOccurrence.Task;

        public Task<RuntimeEventOccurrence> SecondOccurrence =>
            _secondOccurrence.Task;

        public int OccurrenceCount =>
            Volatile.Read(
                ref _occurrenceCount);

        public void OnRuntimeEventOccurred(
            RuntimeEventOccurrence occurrence)
        {
            ArgumentNullException.ThrowIfNull(
                occurrence);

            int occurrenceCount =
                Interlocked.Increment(
                    ref _occurrenceCount);

            if (occurrenceCount == 1)
            {
                _firstOccurrence.TrySetResult(
                    occurrence);
            }
            else if (occurrenceCount == 2)
            {
                _secondOccurrence.TrySetResult(
                    occurrence);
            }
        }
    }

    private sealed class QueuedTransportFactory
        : ITransportFactory
    {
        private readonly Queue<ITransportConnection>
            _connections;

        public QueuedTransportFactory(
            params ITransportConnection[] connections)
        {
            ArgumentNullException.ThrowIfNull(
                connections);

            _connections =
                new Queue<ITransportConnection>(
                    connections);
        }

        public int ConnectCallCount
        {
            get;
            private set;
        }

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConnectCallCount++;

            if (!_connections.TryDequeue(
                    out ITransportConnection? connection))
            {
                throw new InvalidOperationException(
                    "No test transport connection remains.");
            }

            return Task.FromResult(
                connection);
        }
    }

    private sealed class TestDuplexTransportConnection
        : ITransportDuplexConnection,
          IAsyncDisposable
    {
        private readonly Channel<byte[]> _receivedFrames =
            Channel.CreateUnbounded<byte[]>(
                new UnboundedChannelOptions
                {
                    SingleReader =
                        true,
                    SingleWriter =
                        false
                });

        private readonly BinaryProtocolPayloadCodec _payloadCodec =
            new();

        private readonly ProtocolEnvelopeByteCodec _envelopeByteCodec =
            new();

        private readonly TaskCompletionSource _receiveStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _receiveStopped =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private TransportConnectionState _state =
            TransportConnectionState.Connected;

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State =>
            _state;

        public Task ReceiveStarted =>
            _receiveStarted.Task;

        public Task ReceiveStopped =>
            _receiveStopped.Task;

        public CancellationToken ReceivedCancellationToken
        {
            get;
            private set;
        }

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "ExchangeAsync should not be used by a duplex coordinator.");
        }

        public Task SendAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "No protocol request is expected by this migration test.");
        }

        public async Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken =
                cancellationToken;

            _receiveStarted.TrySetResult();

            try
            {
                return await _receivedFrames.Reader.ReadAsync(
                    cancellationToken);
            }
            catch
            {
                _receiveStopped.TrySetResult();

                throw;
            }
        }

        public void QueueReceivedMessage(
            ProtocolMessage message)
        {
            ArgumentNullException.ThrowIfNull(
                message);

            ProtocolEnvelope envelope =
                _payloadCodec.Encode(
                    message);

            byte[] frame =
                _envelopeByteCodec.Encode(
                    envelope);

            if (!_receivedFrames.Writer.TryWrite(
                    frame))
            {
                throw new InvalidOperationException(
                    "The notification frame could not be queued.");
            }
        }

        public void Fault(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(
                exception);

            TransitionTo(
                TransportConnectionState.Faulted);

            _receivedFrames.Writer.TryComplete(
                exception);
        }

        public ValueTask DisposeAsync()
        {
            TransitionTo(
                TransportConnectionState.Closed);

            _receivedFrames.Writer.TryComplete();

            return ValueTask.CompletedTask;
        }

        private void TransitionTo(
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