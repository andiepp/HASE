using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Transport;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactPropertyReaderTests
{
    private static readonly DateTimeOffset TimestampUtc =
        new(
            2026,
            7,
            21,
            17,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task ReadAsync_Success_ShouldDecodePropertyValue()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    CompactPropertyReadStatus.Success,
                    value: new byte[]
                    {
                        0x01
                    }));

        var reader =
            CreateReader(
                connection);

        CompactPropertyReadResult result =
            await reader.ReadAsync(
                compactPropertyId: 0x01);

        Assert.Equal(
            CompactPropertyReadStatus.Success,
            result.Status);

        Assert.Equal(
            0x01,
            result.Mapping.CompactPropertyId);

        Assert.NotNull(
            result.Value);

        Assert.True(
            Assert.IsType<bool>(
                result.Value.Value));

        Assert.Equal(
            TimestampUtc,
            result.Value.TimestampUtc);

        Assert.Equal(
            PropertyQuality.Good,
            result.Value.Quality);

        CompactSerialFrame requestFrame =
            Assert.Single(
                connection.Requests);

        CompactReadPropertyRequest request =
            CompactReadPropertyCodec.DecodeRequest(
                requestFrame);

        Assert.Equal(
            0x2A,
            request.CorrelationId);

        Assert.Equal(
            0x01,
            request.PropertyId);
    }

    [Theory]
    [InlineData(
        0x01)]
    [InlineData(
        0x02)]
    public async Task ReadAsync_FailureStatus_ShouldReturnNoValue(
        byte statusByte)
    {
        CompactPropertyReadStatus status =
            (CompactPropertyReadStatus)statusByte;

        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    status,
                    ReadOnlyMemory<byte>.Empty));

        CompactPropertyReadResult result =
            await CreateReader(
                    connection)
                .ReadAsync(
                    compactPropertyId: 0x01);

        Assert.Equal(
            status,
            result.Status);

        Assert.Null(
            result.Value);
    }

    [Fact]
    public async Task ReadAsync_MismatchedCorrelationId_ShouldThrow()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2B,
                    propertyId: 0x01,
                    CompactPropertyReadStatus.Success,
                    value: new byte[]
                    {
                        0x01
                    }));

        async Task Act()
        {
            _ = await CreateReader(
                    connection)
                .ReadAsync(
                    compactPropertyId: 0x01);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task ReadAsync_MismatchedPropertyId_ShouldThrow()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x02,
                    CompactPropertyReadStatus.Success,
                    value: new byte[]
                    {
                        0x01
                    }));

        async Task Act()
        {
            _ = await CreateReader(
                    connection)
                .ReadAsync(
                    compactPropertyId: 0x01);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task ReadAsync_InvalidEncodedValue_ShouldThrow()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    CompactPropertyReadStatus.Success,
                    value: new byte[]
                    {
                        0x02
                    }));

        async Task Act()
        {
            _ = await CreateReader(
                    connection)
                .ReadAsync(
                    compactPropertyId: 0x01);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task ReadAsync_UnknownMappedProperty_ShouldNotExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    CompactPropertyReadStatus.Success,
                    value: new byte[]
                    {
                        0x01
                    }));

        async Task Act()
        {
            _ = await CreateReader(
                    connection)
                .ReadAsync(
                    compactPropertyId: 0x02);
        }

        await Assert.ThrowsAsync<ArgumentException>(
            Act);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public async Task ReadAsync_ZeroPropertyId_ShouldNotExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    CompactPropertyReadStatus.Success,
                    value: new byte[]
                    {
                        0x01
                    }));

        async Task Act()
        {
            _ = await CreateReader(
                    connection)
                .ReadAsync(
                    compactPropertyId: 0);
        }

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            Act);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public async Task ReadAsync_CancelledToken_ShouldNotExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    CompactPropertyReadStatus.Success,
                    value: new byte[]
                    {
                        0x01
                    }));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await CreateReader(
                    connection)
                .ReadAsync(
                    compactPropertyId: 0x01,
                    cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public async Task ReadAsync_ZeroAllocatedCorrelation_ShouldNotExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    correlationId: 0x2A,
                    propertyId: 0x01,
                    CompactPropertyReadStatus.Success,
                    value: new byte[]
                    {
                        0x01
                    }));

        var reader =
            new CompactPropertyReader(
                connection,
                CreatePropertyMap(),
                correlationIdFactory:
                    () => 0,
                utcNowFactory:
                    () => TimestampUtc);

        async Task Act()
        {
            _ = await reader.ReadAsync(
                compactPropertyId: 0x01);
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
            _ = new CompactPropertyReader(
                null!,
                CreatePropertyMap());
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullPropertyMap_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactPropertyReader(
                new TestCompactSerialProtocolConnection(
                    CreateResponse(
                        correlationId: 0x2A,
                        propertyId: 0x01,
                        CompactPropertyReadStatus.Success,
                        value: new byte[]
                        {
                            0x01
                        })),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static CompactPropertyReader CreateReader(
        ICompactSerialProtocolConnection connection)
    {
        return new CompactPropertyReader(
            connection,
            CreatePropertyMap(),
            correlationIdFactory:
                () => 0x2A,
            utcNowFactory:
                () => TimestampUtc);
    }

    private static CompactPropertyMap CreatePropertyMap()
    {
        var instrumentId =
            new InstrumentId(
                "controller-01");

        var propertyId =
            new PropertyId(
                "led-state");

        var property =
            new PropertyDescriptor(
                propertyId,
                new DescriptorPath(
                    "Led",
                    "State"),
                "LED State",
                new BooleanDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.Read
            };

        var instrument =
            new InstrumentDescriptor(
                instrumentId,
                "Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            property
                        ])
            };

        var definition =
            new EndpointDescriptorDefinition(
                metadata:
                    new(),
                instruments:
                [
                    instrument
                ]);

        return new CompactPropertyMap(
            definition,
            mappings:
            [
                new CompactPropertyMapping(
                    compactPropertyId: 0x01,
                    instrumentId,
                    propertyId,
                    CompactPropertyValueEncoding.Boolean)
            ]);
    }

    private static CompactSerialFrame CreateResponse(
        byte correlationId,
        byte propertyId,
        CompactPropertyReadStatus status,
        ReadOnlyMemory<byte> value)
    {
        return CompactReadPropertyCodec.EncodeResponse(
            new CompactReadPropertyResponse(
                correlationId,
                propertyId,
                status,
                value));
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