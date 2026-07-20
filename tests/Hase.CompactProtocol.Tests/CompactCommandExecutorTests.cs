using Hase.Transport;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactCommandExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ValidSuccessResponse_ShouldReturnSuccess()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CompactExecuteCommandCodec.EncodeResponse(
                    new CompactExecuteCommandResponse(
                        correlationId: 0x2A,
                        commandId: 0x01,
                        CompactCommandExecutionStatus.Success)));

        var executor =
            new CompactCommandExecutor(
                connection,
                correlationIdFactory:
                    () => 0x2A);

        CompactCommandExecutionStatus result =
            await executor.ExecuteAsync(
                commandId: 0x01);

        Assert.Equal(
            CompactCommandExecutionStatus.Success,
            result);

        CompactSerialFrame requestFrame =
            Assert.Single(
                connection.Requests);

        CompactExecuteCommandRequest request =
            CompactExecuteCommandCodec.DecodeRequest(
                requestFrame);

        Assert.Equal(
            0x2A,
            request.CorrelationId);

        Assert.Equal(
            0x01,
            request.CommandId);
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x01)]
    [InlineData(0x02)]
    public async Task ExecuteAsync_KnownResponseStatus_ShouldReturnStatus(
        byte statusByte)
    {
        CompactCommandExecutionStatus status =
            (CompactCommandExecutionStatus)statusByte;

        var executor =
            new CompactCommandExecutor(
                new TestCompactSerialProtocolConnection(
                    CompactExecuteCommandCodec.EncodeResponse(
                        new CompactExecuteCommandResponse(
                            correlationId: 0x2A,
                            commandId: 0x01,
                            status))),
                correlationIdFactory:
                    () => 0x2A);

        CompactCommandExecutionStatus result =
            await executor.ExecuteAsync(
                commandId: 0x01);

        Assert.Equal(
            status,
            result);
    }

    [Fact]
    public async Task ExecuteAsync_MismatchedResponseCommandId_ShouldThrow()
    {
        var executor =
            new CompactCommandExecutor(
                new TestCompactSerialProtocolConnection(
                    CompactExecuteCommandCodec.EncodeResponse(
                        new CompactExecuteCommandResponse(
                            correlationId: 0x2A,
                            commandId: 0x02,
                            CompactCommandExecutionStatus.Success))),
                correlationIdFactory:
                    () => 0x2A);

        async Task Act()
        {
            _ = await executor.ExecuteAsync(
                commandId: 0x01);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task ExecuteAsync_WrongResponseType_ShouldThrow()
    {
        var executor =
            new CompactCommandExecutor(
                new TestCompactSerialProtocolConnection(
                    new CompactSerialFrame(
                        (byte)CompactSerialMessageType.BootstrapResponse,
                        correlationId: 0x2A,
                        payload:
                        [
                            0x01,
                            0x00
                        ])),
                correlationIdFactory:
                    () => 0x2A);

        async Task Act()
        {
            _ = await executor.ExecuteAsync(
                commandId: 0x01);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroCommandId_ShouldThrowWithoutAllocationOrExchange()
    {
        int allocatorCallCount =
            0;

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactExecuteCommandCodec.EncodeResponse(
                    new CompactExecuteCommandResponse(
                        correlationId: 0x2A,
                        commandId: 0x01,
                        CompactCommandExecutionStatus.Success)));

        var executor =
            new CompactCommandExecutor(
                connection,
                correlationIdFactory:
                    () =>
                    {
                        allocatorCallCount++;

                        return 0x2A;
                    });

        async Task Act()
        {
            _ = await executor.ExecuteAsync(
                commandId: 0);
        }

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            Act);

        Assert.Equal(
            0,
            allocatorCallCount);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_ShouldNotAllocateOrExchange()
    {
        int allocatorCallCount =
            0;

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactExecuteCommandCodec.EncodeResponse(
                    new CompactExecuteCommandResponse(
                        correlationId: 0x2A,
                        commandId: 0x01,
                        CompactCommandExecutionStatus.Success)));

        var executor =
            new CompactCommandExecutor(
                connection,
                correlationIdFactory:
                    () =>
                    {
                        allocatorCallCount++;

                        return 0x2A;
                    });

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await executor.ExecuteAsync(
                commandId: 0x01,
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            0,
            allocatorCallCount);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroAllocatedCorrelation_ShouldThrowWithoutExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CompactExecuteCommandCodec.EncodeResponse(
                    new CompactExecuteCommandResponse(
                        correlationId: 0x2A,
                        commandId: 0x01,
                        CompactCommandExecutionStatus.Success)));

        var executor =
            new CompactCommandExecutor(
                connection,
                correlationIdFactory:
                    () => 0);

        async Task Act()
        {
            _ = await executor.ExecuteAsync(
                commandId: 0x01);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            Act);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public void Constructor_NullConnection_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactCommandExecutor(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullCorrelationFactory_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactCommandExecutor(
                new TestCompactSerialProtocolConnection(
                    CompactExecuteCommandCodec.EncodeResponse(
                        new CompactExecuteCommandResponse(
                            correlationId: 0x2A,
                            commandId: 0x01,
                            CompactCommandExecutionStatus.Success))),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private sealed class TestCompactSerialProtocolConnection
        : ICompactSerialProtocolConnection
    {
        private readonly CompactSerialFrame _response;

        public TestCompactSerialProtocolConnection(
            CompactSerialFrame response)
        {
            _response =
                response;
        }

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

        public List<CompactSerialFrame> Requests
        {
            get;
        } =
            [];

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Requests.Add(
                request);

            return Task.FromResult(
                _response);
        }

        public void Invalidate()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
