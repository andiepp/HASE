using System.Threading.Channels;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionCoordinatorNotificationTests
{
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

        await using (
            var coordinator =
                new RuntimeEndpointConnectionCoordinator(
                    connectionManager,
                    runtimeEndpoint,
                    synchronizer))
        {
            coordinator.SubscribeNotification(
                observer);

            await coordinator.ConnectAsync();

            // Act
            transportConnection.QueueReceivedMessage(
                expectedNotification);

            ProtocolMessage actualNotification =
                await observer.NotificationReceived;

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

        await transportConnection.ReceiveStopped;
    }

    private static EventNotification CreateNotification()
    {
        return new EventNotification(
            new InstrumentId(
                "controller-01"),
            new DescriptorPath(
                "Controller",
                "ButtonPressed"),
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
        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "coordinator-notification-endpoint"))
            {
                Metadata =
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Coordinator Notification Endpoint",
                        Description =
                            "Endpoint used to verify coordinator-level "
                            + "notification delivery."
                    }
            };

        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            descriptor);
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