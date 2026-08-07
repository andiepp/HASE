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

public sealed class RuntimeEndpointConnectionCoordinatorNotificationTests
{
    private static readonly TimeSpan TestTimeout =
        TimeSpan.FromSeconds(
            15);

    private static readonly InstrumentId ControllerInstrumentId =
        new(
            "controller-01");

    private static readonly DescriptorPath ButtonPressedPath =
        new(
            "Controller",
            "ButtonPressed");

    [Fact]
    public async Task SubscribeBeforeConnect_EventNotification_ShouldDeliver()
    {
        // Arrange
        var transportConnection =
            new TestDuplexTransportConnection();

        var transportFactory =
            new TestTransportFactory(
                transportConnection);

        await using var connectionManager =
            new TransportConnectionManager(
                transportFactory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestProtocolSynchronizer();

        var observer =
            new RecordingNotificationObserver();

        EventNotification expectedNotification =
            CreateNotification();

        var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                synchronizer);
        try
        {
            coordinator.SubscribeNotification(
                observer);

            await coordinator.ConnectAsync();

            await transportConnection.ReceiveStarted.WaitAsync(
                TestTimeout);

            // Act
            transportConnection.QueueReceivedMessage(
                expectedNotification);

            ProtocolMessage actualNotification =
                await observer.NotificationReceived.WaitAsync(
                    TestTimeout);

            // Assert
            Assert.Equal(
                expectedNotification,
                actualNotification);

            Assert.Equal(
                1,
                observer.NotificationCount);

            Assert.Equal(
                EndpointConnectionState.Ready,
                runtimeEndpoint.ConnectionStatus.State);
        }
        finally
        {
            await coordinator.DisposeAsync().AsTask().WaitAsync(
                TestTimeout);
        }

        await transportConnection.ReceiveStopped.WaitAsync(
            TestTimeout);
    }

    [Fact]
    public async Task ConnectAsync_MatchingEventNotification_ShouldPublishRuntimeEvent()
    {
        // Arrange
        var transportConnection =
            new TestDuplexTransportConnection();

        var transportFactory =
            new TestTransportFactory(
                transportConnection);

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

        EventNotification notification =
            CreateNotification();

        var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                synchronizer);
        try
        {
            await coordinator.ConnectAsync();

            await transportConnection.ReceiveStarted.WaitAsync(
                TestTimeout);

            // Act
            transportConnection.QueueReceivedMessage(
                notification);

            RuntimeEventOccurrence occurrence =
                await eventObserver.EventOccurred.WaitAsync(
                    TestTimeout);

            // Assert
            Assert.Same(
                runtimeEvent,
                occurrence.Event);

            Assert.Equal(
                notification.TimestampUtc,
                occurrence.TimestampUtc);

            Assert.Null(
                occurrence.Value);

            Assert.Equal(
                1,
                eventObserver.OccurrenceCount);

            Assert.Equal(
                EndpointConnectionState.Ready,
                runtimeEndpoint.ConnectionStatus.State);
        }
        finally
        {
            await coordinator.DisposeAsync().AsTask().WaitAsync(
                TestTimeout);
        }

        runtimeEvent.Unsubscribe(
            eventObserver);

        await transportConnection.ReceiveStopped.WaitAsync(
            TestTimeout);
    }

    private static EventNotification CreateNotification()
    {
        return new EventNotification(
            ControllerInstrumentId,
            ButtonPressedPath,
            new DateTimeOffset(
                2026,
                7,
                18,
                12,
                0,
                0,
                TimeSpan.Zero),
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

        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "coordinator-notification-endpoint"),
                [
                    controllerInstrumentDescriptor
                ])
            {
                Metadata =
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Coordinator Notification Endpoint",
                        Description =
                            "Endpoint used to verify coordinator-level "
                            + "notification delivery and runtime event routing."
                    }
            };

        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            descriptor);
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

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNotificationObserver
        : IProtocolNotificationObserver
    {
        private readonly TaskCompletionSource<ProtocolMessage>
            _notificationReceived =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ProtocolMessage> NotificationReceived =>
            _notificationReceived.Task;

        public int NotificationCount
        {
            get;
            private set;
        }

        public void OnProtocolNotification(
            ProtocolMessage notification)
        {
            ArgumentNullException.ThrowIfNull(
                notification);

            NotificationCount++;

            _notificationReceived.TrySetResult(
                notification);
        }
    }

    private sealed class RecordingRuntimeEventObserver
        : IRuntimeEventObserver
    {
        private readonly TaskCompletionSource<RuntimeEventOccurrence>
            _eventOccurred =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<RuntimeEventOccurrence> EventOccurred =>
            _eventOccurred.Task;

        public int OccurrenceCount
        {
            get;
            private set;
        }

        public void OnRuntimeEventOccurred(
            RuntimeEventOccurrence occurrence)
        {
            ArgumentNullException.ThrowIfNull(
                occurrence);

            OccurrenceCount++;

            _eventOccurred.TrySetResult(
                occurrence);
        }
    }

    private sealed class TestTransportFactory
        : ITransportFactory
    {
        private readonly ITransportConnection _connection;

        public TestTransportFactory(
            ITransportConnection connection)
        {
            _connection =
                connection
                ?? throw new ArgumentNullException(
                    nameof(connection));
        }

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _connection);
        }
    }

    private sealed class TestDuplexTransportConnection
        : ITransportDuplexConnection
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

        private readonly TaskCompletionSource _receiveStopped =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _receiveStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

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

        public Task ReceiveStopped =>
            _receiveStopped.Task;

        public Task ReceiveStarted =>
            _receiveStarted.Task;

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
                "No protocol request is expected by this notification test.");
        }

        public async Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            _receiveStarted.TrySetResult();

            try
            {
                return await _receivedFrames.Reader.ReadAsync(
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                _receiveStopped.TrySetResult();

                throw;
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
    }
}
