using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using System.Threading.Channels;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class
    RuntimeEndpointConnectionCoordinatorDuplexDiagnosticsTests
{
    [Fact]
    public async Task ConnectAsync_DuplexExchange_ShouldCollectLogicalStatistics()
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
            TransportExchangeStatistics statistics =
                coordinator.GetExchangeStatistics();

            Assert.Equal(
                1,
                statistics.CompletedExchangeCount);

            Assert.Equal(
                1,
                statistics.SuccessfulExchangeCount);

            Assert.Equal(
                0,
                statistics.FailedExchangeCount);

            Assert.Equal(
                0,
                statistics.CancelledExchangeCount);

            Assert.Equal(
                transportConnection.LastRequestByteCount,
                statistics.TotalRequestByteCount);

            Assert.Equal(
                transportConnection.LastResponseByteCount,
                statistics.TotalResponseByteCount);

            Assert.True(
                statistics.TotalRequestByteCount > 0);

            Assert.True(
                statistics.TotalResponseByteCount > 0);

            Assert.True(
                statistics.TotalDuration >= TimeSpan.Zero);

            Assert.NotNull(
                statistics.LastCompletedAtUtc);

            Assert.Equal(
                TimeSpan.Zero,
                statistics.LastCompletedAtUtc!.Value.Offset);

            Assert.Equal(
                TransportExchangeOutcome.Succeeded,
                statistics.LastOutcome);

            Assert.Equal(
                TransportExchangeStatistics.Empty,
                connectionManager.GetExchangeStatistics());

            Assert.Equal(
                EndpointConnectionState.Ready,
                runtimeEndpoint.ConnectionStatus.State);
        }

        await transportConnection.ReceiveStopped;
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "coordinator-duplex-diagnostics-endpoint"))
            {
                Metadata =
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Coordinator Duplex Diagnostics Endpoint",
                        Description =
                            "Endpoint used to verify logical duplex "
                            + "exchange diagnostics."
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

            var request =
                new DiscoverRequest(
                    new CorrelationId(
                        1));

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
                    new EndpointId(
                        "physical-endpoint"),
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
        }
    }
}