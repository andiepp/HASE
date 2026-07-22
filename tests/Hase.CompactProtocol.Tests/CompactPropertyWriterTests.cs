using Hase.Transport;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactPropertyWriterTests
{
    [Fact]
    public async Task WriteAsync_Success_ShouldSendValueAndReturnSuccess()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    CompactPropertyWriteStatus.Success));

        var writer =
            CreateWriter(
                connection);

        CompactPropertyWriteStatus result =
            await writer.WriteAsync(
                propertyId: 0x01,
                value: new byte[]
                {
                    0x01
                });

        Assert.Equal(
            CompactPropertyWriteStatus.Success,
            result);

        CompactSerialFrame requestFrame =
            Assert.Single(
                connection.Requests);

        CompactWritePropertyRequest request =
            CompactWritePropertyCodec.DecodeRequest(
                requestFrame);

        Assert.Equal(
            0x2A,
            request.CorrelationId);

        Assert.Equal(
            0x01,
            request.PropertyId);

        Assert.Equal(
            new byte[]
            {
                0x01
            },
            request.Value.ToArray());
    }

    [Theory]
    [InlineData(
        0x00)]
    [InlineData(
        0x01)]
    [InlineData(
        0x02)]
    [InlineData(
        0x03)]
    [InlineData(
        0x04)]
    public async Task WriteAsync_DefinedResponseStatus_ShouldReturnStatus(
        byte statusByte)
    {
        CompactPropertyWriteStatus status =
            (CompactPropertyWriteStatus)statusByte;

        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    status));

        CompactPropertyWriteStatus result =
            await CreateWriter(
                    connection)
                .WriteAsync(
                    propertyId: 0x01,
                    value: new byte[]
                    {
                        0x01
                    });

        Assert.Equal(
            status,
            result);
    }

    [Fact]
    public async Task WriteAsync_MismatchedCorrelationId_ShouldThrow()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2B,
                    propertyId: 0x01,
                    CompactPropertyWriteStatus.Success));

        async Task Act()
        {
            _ = await CreateWriter(
                    connection)
                .WriteAsync(
                    propertyId: 0x01,
                    value: new byte[]
                    {
                        0x01
                    });
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task WriteAsync_MismatchedPropertyId_ShouldThrow()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x02,
                    CompactPropertyWriteStatus.Success));

        async Task Act()
        {
            _ = await CreateWriter(
                    connection)
                .WriteAsync(
                    propertyId: 0x01,
                    value: new byte[]
                    {
                        0x01
                    });
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task WriteAsync_WrongResponseType_ShouldThrow()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                new CompactSerialFrame(
                    (byte)CompactSerialMessageType.ReadPropertyResponse,
                    correlationId: 0x2A,
                    payload:
                    [
                        0x01,
                        0x00,
                        0x01
                    ]));

        async Task Act()
        {
            _ = await CreateWriter(
                    connection)
                .WriteAsync(
                    propertyId: 0x01,
                    value: new byte[]
                    {
                        0x01
                    });
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task WriteAsync_ZeroPropertyId_ShouldNotAllocateOrExchange()
    {
        int allocatorCallCount =
            0;

        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    CompactPropertyWriteStatus.Success));

        var writer =
            new CompactPropertyWriter(
                connection,
                correlationIdFactory:
                    () =>
                    {
                        allocatorCallCount++;

                        return 0x2A;
                    });

        async Task Act()
        {
            _ = await writer.WriteAsync(
                propertyId: 0,
                value: new byte[]
                {
                    0x01
                });
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
    public async Task WriteAsync_EmptyValue_ShouldNotAllocateOrExchange()
    {
        int allocatorCallCount =
            0;

        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    CompactPropertyWriteStatus.Success));

        var writer =
            new CompactPropertyWriter(
                connection,
                correlationIdFactory:
                    () =>
                    {
                        allocatorCallCount++;

                        return 0x2A;
                    });

        async Task Act()
        {
            _ = await writer.WriteAsync(
                propertyId: 0x01,
                ReadOnlyMemory<byte>.Empty);
        }

        await Assert.ThrowsAsync<ArgumentException>(
            Act);

        Assert.Equal(
            0,
            allocatorCallCount);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public async Task WriteAsync_CancelledToken_ShouldNotAllocateOrExchange()
    {
        int allocatorCallCount =
            0;

        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    CompactPropertyWriteStatus.Success));

        var writer =
            new CompactPropertyWriter(
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
            _ = await writer.WriteAsync(
                propertyId: 0x01,
                value: new byte[]
                {
                    0x01
                },
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
    public async Task WriteAsync_ZeroAllocatedCorrelation_ShouldNotExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    CompactPropertyWriteStatus.Success));

        var writer =
            new CompactPropertyWriter(
                connection,
                correlationIdFactory:
                    () => 0);

        async Task Act()
        {
            _ = await writer.WriteAsync(
                propertyId: 0x01,
                value: new byte[]
                {
                    0x01
                });
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
            _ = new CompactPropertyWriter(
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
            _ = new CompactPropertyWriter(
                new TestCompactSerialProtocolConnection(
                    CreateResponse(
                        correlationId: 0x2A,
                        propertyId: 0x01,
                        CompactPropertyWriteStatus.Success)),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static CompactPropertyWriter CreateWriter(
        ICompactSerialProtocolConnection connection)
    {
        return new CompactPropertyWriter(
            connection,
            correlationIdFactory:
                () => 0x2A);
    }

    private static CompactSerialFrame CreateResponse(
        byte correlationId,
        byte propertyId,
        CompactPropertyWriteStatus status)
    {
        return CompactWritePropertyCodec.EncodeResponse(
            new CompactWritePropertyResponse(
                correlationId,
                propertyId,
                status));
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