using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Transport;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactMappedPropertyWriterTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly PropertyId PropertyId =
        new(
            "led-state");

    [Fact]
    public async Task WriteAsync_WritableBoolean_ShouldEncodeAndExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    CompactPropertyWriteStatus.Success));

        CompactMappedPropertyWriter writer =
            CreateWriter(
                connection,
                PropertyAccessMode.ReadWrite);

        CompactPropertyWriteResult result =
            await writer.WriteAsync(
                compactPropertyId: 0x01,
                value: true);

        Assert.Equal(
            CompactPropertyWriteStatus.Success,
            result.Status);

        Assert.Equal(
            0x01,
            result.Mapping.CompactPropertyId);

        Assert.Equal(
            InstrumentId,
            result.Mapping.InstrumentId);

        Assert.Equal(
            PropertyId,
            result.Mapping.PropertyId);

        CompactSerialFrame requestFrame =
            Assert.Single(
                connection.Requests);

        CompactWritePropertyRequest request =
            CompactWritePropertyCodec.DecodeRequest(
                requestFrame);

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

    [Fact]
    public async Task WriteAsync_FalseBoolean_ShouldEncodeZero()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    CompactPropertyWriteStatus.Success));

        _ = await CreateWriter(
                connection,
                PropertyAccessMode.Write)
            .WriteAsync(
                compactPropertyId: 0x01,
                value: false);

        CompactWritePropertyRequest request =
            CompactWritePropertyCodec.DecodeRequest(
                Assert.Single(
                    connection.Requests));

        Assert.Equal(
            new byte[]
            {
                0x00
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
    public async Task WriteAsync_DefinedEndpointStatus_ShouldReturnStatus(
        byte statusByte)
    {
        CompactPropertyWriteStatus status =
            (CompactPropertyWriteStatus)statusByte;

        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    status));

        CompactPropertyWriteResult result =
            await CreateWriter(
                    connection,
                    PropertyAccessMode.ReadWrite)
                .WriteAsync(
                    compactPropertyId: 0x01,
                    value: true);

        Assert.Equal(
            status,
            result.Status);
    }

    [Fact]
    public async Task WriteAsync_ReadOnlyProperty_ShouldNotExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    CompactPropertyWriteStatus.Success));

        async Task Act()
        {
            _ = await CreateWriter(
                    connection,
                    PropertyAccessMode.Read)
                .WriteAsync(
                    compactPropertyId: 0x01,
                    value: true);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            Act);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public async Task WriteAsync_NoneAccessProperty_ShouldNotExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    CompactPropertyWriteStatus.Success));

        async Task Act()
        {
            _ = await CreateWriter(
                    connection,
                    PropertyAccessMode.None)
                .WriteAsync(
                    compactPropertyId: 0x01,
                    value: true);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            Act);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public async Task WriteAsync_InvalidBooleanValue_ShouldNotExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    CompactPropertyWriteStatus.Success));

        async Task Act()
        {
            _ = await CreateWriter(
                    connection,
                    PropertyAccessMode.ReadWrite)
                .WriteAsync(
                    compactPropertyId: 0x01,
                    value: 1);
        }

        await Assert.ThrowsAsync<ArgumentException>(
            Act);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public async Task WriteAsync_UnknownMappedProperty_ShouldNotExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    CompactPropertyWriteStatus.Success));

        async Task Act()
        {
            _ = await CreateWriter(
                    connection,
                    PropertyAccessMode.ReadWrite)
                .WriteAsync(
                    compactPropertyId: 0x02,
                    value: true);
        }

        await Assert.ThrowsAsync<ArgumentException>(
            Act);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public async Task WriteAsync_ZeroPropertyId_ShouldNotExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    CompactPropertyWriteStatus.Success));

        async Task Act()
        {
            _ = await CreateWriter(
                    connection,
                    PropertyAccessMode.ReadWrite)
                .WriteAsync(
                    compactPropertyId: 0,
                    value: true);
        }

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            Act);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public async Task WriteAsync_CancelledToken_ShouldNotExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CreateResponse(
                    CompactPropertyWriteStatus.Success));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await CreateWriter(
                    connection,
                    PropertyAccessMode.ReadWrite)
                .WriteAsync(
                    compactPropertyId: 0x01,
                    value: true,
                    cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public void Result_NullMapping_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactPropertyWriteResult(
                null!,
                CompactPropertyWriteStatus.Success);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Result_UndefinedStatus_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactPropertyWriteResult(
                CreateMapping(),
                (CompactPropertyWriteStatus)0xFF);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Constructor_NullConnection_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactMappedPropertyWriter(
                (ICompactSerialProtocolConnection)null!,
                CreatePropertyMap(
                    PropertyAccessMode.ReadWrite));
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullPropertyMap_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactMappedPropertyWriter(
                new TestCompactSerialProtocolConnection(
                    CreateResponse(
                        CompactPropertyWriteStatus.Success)),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static CompactMappedPropertyWriter CreateWriter(
        ICompactSerialProtocolConnection connection,
        PropertyAccessMode accessMode)
    {
        var rawWriter =
            new CompactPropertyWriter(
                connection,
                correlationIdFactory:
                    () => 0x2A);

        return new CompactMappedPropertyWriter(
            rawWriter,
            CreatePropertyMap(
                accessMode));
    }

    private static CompactPropertyMap CreatePropertyMap(
        PropertyAccessMode accessMode)
    {
        var property =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath(
                    "Led",
                    "State"),
                "LED State",
                new BooleanDataDescriptor())
            {
                AccessMode =
                    accessMode
            };

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Arduino Uno GPIO Controller",
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
                CreateMapping()
            ]);
    }

    private static CompactPropertyMapping CreateMapping()
    {
        return new CompactPropertyMapping(
            compactPropertyId: 0x01,
            InstrumentId,
            PropertyId,
            CompactPropertyValueEncoding.Boolean);
    }

    private static CompactSerialFrame CreateResponse(
        CompactPropertyWriteStatus status)
    {
        return CompactWritePropertyCodec.EncodeResponse(
            new CompactWritePropertyResponse(
                correlationId: 0x2A,
                propertyId: 0x01,
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