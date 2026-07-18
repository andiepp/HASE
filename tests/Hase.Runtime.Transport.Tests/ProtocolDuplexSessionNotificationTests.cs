using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Transport;
using System.Threading.Channels;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolDuplexSessionNotificationTests
{
    [Fact]
    public async Task RunAsync_EventNotification_ShouldPublishToObserver()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        var observer =
            new RecordingObserver();

        session.SubscribeNotification(
            observer);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        EventNotification expectedNotification =
            CreateNotification();

        // Act
        connection.QueueReceivedMessage(
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

        Assert.True(
            session.IsRunning);

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    [Fact]
    public async Task SubscribeNotification_SameObserverTwice_ShouldPublishOnce()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        var observer =
            new RecordingObserver();

        session.SubscribeNotification(
            observer);

        session.SubscribeNotification(
            observer);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        // Act
        connection.QueueReceivedMessage(
            CreateNotification());

        _ =
            await observer.NotificationReceived;

        // Assert
        Assert.Equal(
            1,
            observer.NotificationCount);

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    [Fact]
    public async Task UnsubscribeNotification_ShouldStopDelivery()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        var removedObserver =
            new CountingObserver();

        var activeObserver =
            new RecordingObserver();

        session.SubscribeNotification(
            removedObserver);

        session.SubscribeNotification(
            activeObserver);

        session.UnsubscribeNotification(
            removedObserver);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        // Act
        connection.QueueReceivedMessage(
            CreateNotification());

        _ =
            await activeObserver.NotificationReceived;

        // Assert
        Assert.Equal(
            0,
            removedObserver.NotificationCount);

        Assert.Equal(
            1,
            activeObserver.NotificationCount);

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    [Fact]
    public async Task RunAsync_ObserverThrows_ShouldContinueWithLaterObservers()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        var throwingObserver =
            new ThrowingObserver();

        var recordingObserver =
            new RecordingObserver();

        session.SubscribeNotification(
            throwingObserver);

        session.SubscribeNotification(
            recordingObserver);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        EventNotification expectedNotification =
            CreateNotification();

        // Act
        connection.QueueReceivedMessage(
            expectedNotification);

        ProtocolMessage actualNotification =
            await recordingObserver.NotificationReceived;

        // Assert
        Assert.Equal(
            expectedNotification,
            actualNotification);

        Assert.Equal(
            1,
            throwingObserver.NotificationCount);

        Assert.Equal(
            1,
            recordingObserver.NotificationCount);

        Assert.True(
            session.IsRunning);

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    [Fact]
    public async Task RunAsync_NotificationWhileRequestPending_ShouldPublishAndThenRouteResponse()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        var observer =
            new RecordingObserver();

        session.SubscribeNotification(
            observer);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        var correlationId =
            new CorrelationId(
                801);

        Task<ProtocolMessage> responseTask =
            session.SendAsync(
                new DiscoverRequest(
                    correlationId));

        ProtocolMessage sentRequest =
            await connection.ReadSentMessageAsync();

        EventNotification expectedNotification =
            CreateNotification();

        // Act
        connection.QueueReceivedMessage(
            expectedNotification);

        ProtocolMessage actualNotification =
            await observer.NotificationReceived;

        Assert.False(
            responseTask.IsCompleted);

        connection.QueueReceivedMessage(
            new DiscoverResponse(
                correlationId,
                new EndpointId(
                    "Endpoint-1"),
                []));

        ProtocolMessage response =
            await responseTask;

        // Assert
        Assert.Equal(
            correlationId,
            sentRequest.CorrelationId);

        Assert.Equal(
            expectedNotification,
            actualNotification);

        Assert.Equal(
            correlationId,
            response.CorrelationId);

        Assert.IsType<DiscoverResponse>(
            response);

        Assert.True(
            session.IsRunning);

        await StopAsync(
            cancellationTokenSource,
            runTask);
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
                6,
                0,
                0,
                TimeSpan.Zero),
            null);
    }

    private static async Task StopAsync(
        CancellationTokenSource cancellationTokenSource,
        Task runTask)
    {
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await runTask);
    }

    private sealed class RecordingObserver
        : IProtocolNotificationObserver
    {
        private readonly TaskCompletionSource<ProtocolMessage>
            _notificationReceived =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public int NotificationCount
        {
            get;
            private set;
        }

        public Task<ProtocolMessage> NotificationReceived =>
            _notificationReceived.Task;

        public void OnProtocolNotification(
            ProtocolMessage notification)
        {
            NotificationCount++;

            _notificationReceived.TrySetResult(
                notification);
        }
    }

    private sealed class CountingObserver
        : IProtocolNotificationObserver
    {
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
        }
    }

    private sealed class ThrowingObserver
        : IProtocolNotificationObserver
    {
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

            throw new InvalidOperationException(
                "Expected observer failure.");
        }
    }

    private sealed class TestDuplexTransportConnection
        : ITransportDuplexConnection
    {
        private readonly Channel<byte[]> _sentFrames =
            Channel.CreateUnbounded<byte[]>();

        private readonly Channel<byte[]> _receivedFrames =
            Channel.CreateUnbounded<byte[]>();

        private readonly BinaryProtocolPayloadCodec _payloadCodec =
            new();

        private readonly ProtocolEnvelopeByteCodec _envelopeByteCodec =
            new();

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

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "The test connection uses duplex operations.");
        }

        public async Task SendAsync(
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                payload);

            cancellationToken.ThrowIfCancellationRequested();

            await _sentFrames.Writer.WriteAsync(
                payload,
                cancellationToken);
        }

        public async Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            return await _receivedFrames.Reader.ReadAsync(
                cancellationToken);
        }

        public async Task<ProtocolMessage> ReadSentMessageAsync()
        {
            byte[] frame =
                await _sentFrames.Reader.ReadAsync();

            ProtocolEnvelope envelope =
                _envelopeByteCodec.Decode(
                    frame);

            return _payloadCodec.Decode(
                envelope);
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

            bool written =
                _receivedFrames.Writer.TryWrite(
                    frame);

            Assert.True(
                written);
        }
    }
}