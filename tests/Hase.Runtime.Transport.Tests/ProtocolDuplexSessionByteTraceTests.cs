using System.Threading.Channels;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Diagnostics;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolDuplexSessionByteTraceTests
{
    [Fact]
    public async Task Exchange_PublishesExactOutboundAndInboundFrames()
    {
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        var observer =
            new RecordingByteTraceObserver();

        session.SubscribeByteTrace(
            observer);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        var correlationId =
            new CorrelationId(
                42);

        Task<ProtocolMessage> responseTask =
            session.SendAsync(
                new DiscoverRequest(
                    correlationId));

        byte[] requestFrame =
            await connection.ReadSentFrameAsync();

        _ =
            await observer.WaitForTraceAsync();

        byte[] responseFrame =
            connection.QueueReceivedMessage(
                new DiscoverResponse(
                    correlationId,
                    new EndpointId(
                        "endpoint-one"),
                    []));

        _ =
            await responseTask;

        Assert.Collection(
            observer.Traces,
            outbound =>
            {
                Assert.Equal(
                    TransportByteDirection.Outbound,
                    outbound.Direction);
                Assert.Equal(
                    "42",
                    outbound.CorrelationId);
                Assert.Equal(
                    requestFrame,
                    outbound.Bytes);
            },
            inbound =>
            {
                Assert.Equal(
                    TransportByteDirection.Inbound,
                    inbound.Direction);
                Assert.Equal(
                    "42",
                    inbound.CorrelationId);
                Assert.Equal(
                    responseFrame,
                    inbound.Bytes);
            });

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    [Fact]
    public async Task Notification_PublishesInboundFrameWithNoCorrelation()
    {
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        var observer =
            new RecordingByteTraceObserver();

        session.SubscribeByteTrace(
            observer);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        byte[] notificationFrame =
            connection.QueueReceivedMessage(
                new EventNotification(
                    new InstrumentId(
                        "instrument-one"),
                    new DescriptorPath(
                        "Button",
                        "Pressed"),
                    DateTimeOffset.UtcNow,
                    Value: null));

        RecordedTrace trace =
            await observer.WaitForTraceAsync();

        Assert.Equal(
            TransportByteDirection.Inbound,
            trace.Direction);
        Assert.Null(
            trace.CorrelationId);
        Assert.Equal(
            notificationFrame,
            trace.Bytes);

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    [Fact]
    public async Task MalformedInboundFrame_StillTracesWithoutChangingFailure()
    {
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        var observer =
            new RecordingByteTraceObserver();

        session.SubscribeByteTrace(
            observer);

        Task runTask =
            session.RunAsync();

        byte[] malformed =
        [
            0x01,
            0x02
        ];

        connection.QueueReceivedFrame(
            malformed);

        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                    await runTask);

        Assert.NotNull(
            exception);

        RecordedTrace trace =
            Assert.Single(
                observer.Traces);

        Assert.Null(
            trace.CorrelationId);
        Assert.Equal(
            malformed,
            trace.Bytes);
    }

    [Fact]
    public async Task ThrowingObserver_DoesNotChangeExchange()
    {
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        session.SubscribeByteTrace(
            new ThrowingByteTraceObserver());

        var observer =
            new RecordingByteTraceObserver();

        session.SubscribeByteTrace(
            observer);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        var correlationId =
            new CorrelationId(
                42);

        Task<ProtocolMessage> responseTask =
            session.SendAsync(
                new DiscoverRequest(
                    correlationId));

        _ =
            await connection.ReadSentFrameAsync();

        connection.QueueReceivedMessage(
            new DiscoverResponse(
                correlationId,
                new EndpointId(
                    "endpoint-one"),
                []));

        Assert.IsType<DiscoverResponse>(
            await responseTask);
        Assert.Equal(
            2,
            observer.Traces.Count);

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    [Fact]
    public async Task UnsubscribedObserver_ReceivesNoFrames()
    {
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        var observer =
            new RecordingByteTraceObserver();

        session.SubscribeByteTrace(
            observer);
        session.SubscribeByteTrace(
            observer);
        session.UnsubscribeByteTrace(
            observer);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        var correlationId =
            new CorrelationId(
                42);

        Task<ProtocolMessage> responseTask =
            session.SendAsync(
                new DiscoverRequest(
                    correlationId));

        _ =
            await connection.ReadSentFrameAsync();

        connection.QueueReceivedMessage(
            new DiscoverResponse(
                correlationId,
                new EndpointId(
                    "endpoint-one"),
                []));

        _ =
            await responseTask;

        Assert.Empty(
            observer.Traces);

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    [Fact]
    public void NativeObserver_EnabledBytes_PublishesBoundedSnapshot()
    {
        var collector =
            new BoundedRuntimeDiagnosticCollector(
                4,
                RuntimeDiagnosticLevel.Bytes);

        var observer =
            new NativeTransportByteDiagnosticObserver(
                "endpoint-one",
                new RuntimeDiagnosticPublisher(
                    collector));

        byte[] bytes =
            new byte[
                RuntimeDiagnosticByteSnapshot
                    .MaximumCapturedByteCount
                + 1];

        observer.OnTransportBytes(
            new TransportByteTrace(
                TransportByteDirection.Inbound,
                bytes,
                "42"));

        RuntimeDiagnosticRecord record =
            Assert.Single(
                collector.GetSnapshot(
                    RuntimeDiagnosticLevel.Bytes));

        RuntimeDiagnosticByteSnapshot snapshot =
            Assert.IsType<RuntimeDiagnosticByteSnapshot>(
                record.ByteSnapshot);

        Assert.Equal(
            bytes.Length,
            snapshot.OriginalByteCount);
        Assert.Equal(
            RuntimeDiagnosticByteSnapshot.MaximumCapturedByteCount,
            snapshot.CapturedByteCount);
        Assert.True(
            snapshot.IsTruncated);
    }

    [Fact]
    public void NativeObserver_ProtocolOnly_PublishesNothing()
    {
        var collector =
            new BoundedRuntimeDiagnosticCollector(
                4,
                RuntimeDiagnosticLevel.Protocol);

        var observer =
            new NativeTransportByteDiagnosticObserver(
                "endpoint-one",
                new RuntimeDiagnosticPublisher(
                    collector));

        observer.OnTransportBytes(
            new TransportByteTrace(
                TransportByteDirection.Outbound,
                new byte[]
                {
                    0xA5
                },
                "42"));

        Assert.Empty(
            collector.GetSnapshot());
    }

    private static async Task StopAsync(
        CancellationTokenSource cancellationTokenSource,
        Task runTask)
    {
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await runTask);
    }

    private sealed class RecordingByteTraceObserver
        : ITransportByteTraceObserver
    {
        private readonly TaskCompletionSource<RecordedTrace>
            firstTrace =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        public List<RecordedTrace> Traces
        {
            get;
        } =
        [];

        public void OnTransportBytes(
            TransportByteTrace trace)
        {
            var recorded =
                new RecordedTrace(
                    trace.Direction,
                    trace.Bytes.ToArray(),
                    trace.CorrelationId);

            Traces.Add(
                recorded);

            firstTrace.TrySetResult(
                recorded);
        }

        public Task<RecordedTrace> WaitForTraceAsync()
        {
            return firstTrace.Task;
        }
    }

    private sealed record RecordedTrace(
        TransportByteDirection Direction,
        byte[] Bytes,
        string? CorrelationId);

    private sealed class ThrowingByteTraceObserver
        : ITransportByteTraceObserver
    {
        public void OnTransportBytes(
            TransportByteTrace trace)
        {
            throw new InvalidOperationException(
                "observer failure");
        }
    }

    private sealed class TestDuplexTransportConnection
        : ITransportDuplexConnection
    {
        private readonly Channel<byte[]> sentFrames =
            Channel.CreateUnbounded<byte[]>();

        private readonly Channel<byte[]> receivedFrames =
            Channel.CreateUnbounded<byte[]>();

        private readonly BinaryProtocolPayloadCodec payloadCodec =
            new();

        private readonly ProtocolEnvelopeByteCodec envelopeCodec =
            new();

        public event EventHandler<TransportConnectionStateChangedEventArgs>?
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
            throw new NotSupportedException();
        }

        public async Task SendAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            await sentFrames.Writer.WriteAsync(
                request,
                cancellationToken);
        }

        public async Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            return await receivedFrames.Reader.ReadAsync(
                cancellationToken);
        }

        public async Task<byte[]> ReadSentFrameAsync()
        {
            return await sentFrames.Reader.ReadAsync();
        }

        public byte[] QueueReceivedMessage(
            ProtocolMessage message)
        {
            byte[] frame =
                envelopeCodec.Encode(
                    payloadCodec.Encode(
                        message));

            QueueReceivedFrame(
                frame);

            return frame;
        }

        public void QueueReceivedFrame(
            byte[] frame)
        {
            Assert.True(
                receivedFrames.Writer.TryWrite(
                    frame));
        }
    }
}
