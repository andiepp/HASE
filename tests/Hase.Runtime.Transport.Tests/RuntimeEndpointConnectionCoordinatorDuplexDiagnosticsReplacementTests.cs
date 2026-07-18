using System.Threading.Channels;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class
    RuntimeEndpointConnectionCoordinatorDuplexDiagnosticsReplacementTests
{
    [Fact]
    public async Task ReconnectAsync_ShouldAggregateLogicalSessionStatistics()
    {
        // Arrange
        var initialConnection =
            new TestDuplexTransportConnection(
                "physical-endpoint-01");

        var replacementConnection =
            new TestDuplexTransportConnection(
                "physical-endpoint-02");

        var transportFactory =
            new QueuedTransportFactory(
                initialConnection,
                replacementConnection);

        await using var connectionManager =
            new TransportConnectionManager(
                transportFactory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new DiscoveringProtocolSynchronizer();

        await using (
            var coordinator =
                new RuntimeEndpointConnectionCoordinator(
                    connectionManager,
                    runtimeEndpoint,
                    synchronizer))
        {
            // Act
            await coordinator.ConnectAsync();

            // Assert
            TransportExchangeStatistics initialStatistics =
                coordinator.GetExchangeStatistics();

            Assert.Equal(
                1,
                initialStatistics.CompletedExchangeCount);

            Assert.Equal(
                1,
                initialStatistics.SuccessfulExchangeCount);

            // Act
            initialConnection.Fault(
                new IOException(
                    "The simulated initial receive pump failed."));

            await initialConnection.ReceiveStopped;

            Assert.Equal(
                EndpointConnectionState.Faulted,
                runtimeEndpoint.ConnectionStatus.State);

            await coordinator.ReconnectAsync();

            // Assert
            TransportExchangeStatistics aggregateStatistics =
                coordinator.GetExchangeStatistics();

            Assert.Equal(
                2,
                aggregateStatistics.CompletedExchangeCount);

            Assert.Equal(
                2,
                aggregateStatistics.SuccessfulExchangeCount);

            Assert.Equal(
                0,
                aggregateStatistics.FailedExchangeCount);

            Assert.Equal(
                0,
                aggregateStatistics.CancelledExchangeCount);

            Assert.Equal(
                initialConnection.LastRequestByteCount
                + replacementConnection.LastRequestByteCount,
                aggregateStatistics.TotalRequestByteCount);

            Assert.Equal(
                initialConnection.LastResponseByteCount
                + replacementConnection.LastResponseByteCount,
                aggregateStatistics.TotalResponseByteCount);

            Assert.True(
                aggregateStatistics.TotalDuration >= TimeSpan.Zero);

            Assert.NotNull(
                aggregateStatistics.LastCompletedAtUtc);

            Assert.Equal(
                TransportExchangeOutcome.Succeeded,
                aggregateStatistics.LastOutcome);

            Assert.Equal(
                1,
                connectionManager.ReplacementCount);

            Assert.Equal(
                2,
                transportFactory.ConnectCallCount);

            Assert.Equal(
                2,
                synchronizer.SynchronizationCount);

            Assert.Equal(
                EndpointConnectionState.Ready,
                runtimeEndpoint.ConnectionStatus.State);

            Assert.Equal(
                TransportExchangeStatistics.Empty,
                connectionManager.GetExchangeStatistics());
        }

        await replacementConnection.ReceiveStopped;
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "duplex-diagnostics-replacement-endpoint"))
            {
                Metadata =
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Duplex Diagnostics Replacement Endpoint",
                        Description =
                            "Endpoint used to verify logical exchange "
                            + "statistics across replacement sessions."
                    }
            };

        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            descriptor);
    }

    private sealed class DiscoveringProtocolSynchronizer
        : IRuntimeEndpointSynchronizer,
          IRuntimeProtocolEndpointSynchronizer
    {
        private uint _nextCorrelationId;

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

        public async Task SynchronizeAsync(
            IRuntimeProtocolConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                connection);

            ArgumentNullException.ThrowIfNull(
                runtimeEndpoint);

            SynchronizationCount++;

            var request =
                new DiscoverRequest(
                    new CorrelationId(
                        ++_nextCorrelationId));

            ProtocolMessage responseMessage =
                await connection.SendAsync(
                    request,
                    cancellationToken);

            DiscoverResponse response =
                Assert.IsType<DiscoverResponse>(
                    responseMessage);

            Assert.Equal(
                request.CorrelationId,
                response.CorrelationId);
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
        private readonly EndpointId _physicalEndpointId;

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

        private TransportConnectionState _state =
            TransportConnectionState.Connected;

        public TestDuplexTransportConnection(
            string physicalEndpointId)
        {
            _physicalEndpointId =
                new EndpointId(
                    physicalEndpointId);
        }

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State =>
            _state;

        public Task ReceiveStopped =>
            _receiveStopped.Task;

        public int LastRequestByteCount
        {
            get;
            private set;
        }

        public int LastResponseByteCount
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
            ArgumentNullException.ThrowIfNull(
                request);

            cancellationToken.ThrowIfCancellationRequested();

            LastRequestByteCount =
                request.Length;

            ProtocolEnvelope requestEnvelope =
                _envelopeByteCodec.Decode(
                    request);

            ProtocolMessage requestMessage =
                _payloadCodec.Decode(
                    requestEnvelope);

            DiscoverRequest discoverRequest =
                Assert.IsType<DiscoverRequest>(
                    requestMessage);

            var response =
                new DiscoverResponse(
                    discoverRequest.CorrelationId,
                    _physicalEndpointId,
                    Array.Empty<InstrumentId>());

            ProtocolEnvelope responseEnvelope =
                _payloadCodec.Encode(
                    response);

            byte[] responseFrame =
                _envelopeByteCodec.Encode(
                    responseEnvelope);

            LastResponseByteCount =
                responseFrame.Length;

            if (!_receivedFrames.Writer.TryWrite(
                    responseFrame))
            {
                throw new InvalidOperationException(
                    "The test response frame could not be queued.");
            }

            return Task.CompletedTask;
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
            catch (Exception)
                when (_state == TransportConnectionState.Faulted)
            {
                _receiveStopped.TrySetResult();

                throw;
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