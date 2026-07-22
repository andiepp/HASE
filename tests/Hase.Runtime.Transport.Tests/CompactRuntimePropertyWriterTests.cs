using Hase.CompactProtocol;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactRuntimePropertyWriterTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly PropertyId PropertyId =
        new(
            "led-state");

    [Fact]
    public async Task WriteAsync_SuccessfulConfirmation_ShouldUpdateCache()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactPropertyWriteStatus.Success,
                CompactPropertyReadStatus.Success,
                confirmationValue:
                    new byte[]
                    {
                        0x01
                    });

        var writer =
            new CompactRuntimePropertyWriter(
                connection,
                CreatePropertyMap(
                    definition));

        CompactRuntimePropertyWriteResult result =
            await writer.WriteAsync(
                runtimeEndpoint,
                compactPropertyId: 0x01,
                value: true);

        Assert.True(
            result.CacheUpdated);

        Assert.Equal(
            CompactPropertyWriteStatus.Success,
            result.WriteStatus);

        Assert.Equal(
            CompactPropertyReadStatus.Success,
            result.ConfirmationReadStatus);

        Assert.NotNull(
            result.RuntimeProperty.CurrentValue);

        Assert.True(
            Assert.IsType<bool>(
                result.RuntimeProperty.CurrentValue.Value));

        Assert.Equal(
            PropertyQuality.Good,
            result.RuntimeProperty.CurrentValue.Quality);

        Assert.Equal(
            2,
            connection.ExchangeCallCount);

        Assert.Equal(
            new byte[]
            {
                0x01
            },
            connection.WrittenValue.ToArray());
    }

    [Fact]
    public async Task WriteAsync_RejectedWrite_ShouldPreserveCacheWithoutRead()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        RuntimeProperty runtimeProperty =
            FindRuntimeProperty(
                runtimeEndpoint);

        PropertyValue previousValue =
            SetCachedValue(
                runtimeProperty,
                value: false);

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactPropertyWriteStatus.WriteFailed,
                CompactPropertyReadStatus.Success,
                confirmationValue:
                    new byte[]
                    {
                        0x01
                    });

        var writer =
            new CompactRuntimePropertyWriter(
                connection,
                CreatePropertyMap(
                    definition));

        CompactRuntimePropertyWriteResult result =
            await writer.WriteAsync(
                runtimeEndpoint,
                compactPropertyId: 0x01,
                value: true);

        Assert.False(
            result.CacheUpdated);

        Assert.Equal(
            CompactPropertyWriteStatus.WriteFailed,
            result.WriteStatus);

        Assert.Null(
            result.ConfirmationReadStatus);

        Assert.Same(
            previousValue,
            runtimeProperty.CurrentValue);

        Assert.Equal(
            1,
            connection.ExchangeCallCount);
    }

    [Fact]
    public async Task WriteAsync_FailedConfirmation_ShouldPreserveCache()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        RuntimeProperty runtimeProperty =
            FindRuntimeProperty(
                runtimeEndpoint);

        PropertyValue previousValue =
            SetCachedValue(
                runtimeProperty,
                value: false);

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactPropertyWriteStatus.Success,
                CompactPropertyReadStatus.ReadFailed,
                confirmationValue:
                    ReadOnlyMemory<byte>.Empty);

        var writer =
            new CompactRuntimePropertyWriter(
                connection,
                CreatePropertyMap(
                    definition));

        CompactRuntimePropertyWriteResult result =
            await writer.WriteAsync(
                runtimeEndpoint,
                compactPropertyId: 0x01,
                value: true);

        Assert.False(
            result.CacheUpdated);

        Assert.Equal(
            CompactPropertyWriteStatus.Success,
            result.WriteStatus);

        Assert.Equal(
            CompactPropertyReadStatus.ReadFailed,
            result.ConfirmationReadStatus);

        Assert.Same(
            previousValue,
            runtimeProperty.CurrentValue);

        Assert.Equal(
            2,
            connection.ExchangeCallCount);
    }

    [Fact]
    public async Task WriteAsync_UnknownRuntimeInstrument_ShouldNotExchange()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint incompatibleEndpoint =
            new RuntimeContext()
                .AddEndpoint(
                    new EndpointDescriptorDefinition(
                        metadata:
                            new(),
                        instruments:
                            [])
                    .Materialize(
                        new EndpointId(
                            "arduino-uno-01")));

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactPropertyWriteStatus.Success,
                CompactPropertyReadStatus.Success,
                confirmationValue:
                    new byte[]
                    {
                        0x01
                    });

        var writer =
            new CompactRuntimePropertyWriter(
                connection,
                CreatePropertyMap(
                    definition));

        async Task Act()
        {
            _ = await writer.WriteAsync(
                incompatibleEndpoint,
                compactPropertyId: 0x01,
                value: true);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);

        Assert.Equal(
            0,
            connection.ExchangeCallCount);
    }

    [Fact]
    public async Task WriteAsync_CancelledToken_ShouldNotExchange()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactPropertyWriteStatus.Success,
                CompactPropertyReadStatus.Success,
                confirmationValue:
                    new byte[]
                    {
                        0x01
                    });

        var writer =
            new CompactRuntimePropertyWriter(
                connection,
                CreatePropertyMap(
                    definition));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await writer.WriteAsync(
                CreateRuntimeEndpoint(
                    definition),
                compactPropertyId: 0x01,
                value: true,
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            0,
            connection.ExchangeCallCount);
    }

    [Fact]
    public void Constructor_NullConnection_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactRuntimePropertyWriter(
                null!,
                CreatePropertyMap(
                    CreateDefinition()));
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullPropertyMap_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactRuntimePropertyWriter(
                new TestCompactSerialProtocolConnection(
                    CompactPropertyWriteStatus.Success,
                    CompactPropertyReadStatus.Success,
                    confirmationValue:
                        new byte[]
                        {
                            0x01
                        }),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static EndpointDescriptorDefinition CreateDefinition()
    {
        var property =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath(
                    "Led",
                    "State"),
                "Built-in LED State",
                new BooleanDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
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

        return new EndpointDescriptorDefinition(
            metadata:
                new(),
            instruments:
            [
                instrument
            ]);
    }

    private static CompactPropertyMap CreatePropertyMap(
        EndpointDescriptorDefinition definition)
    {
        return new CompactPropertyMap(
            definition,
            mappings:
            [
                new CompactPropertyMapping(
                    compactPropertyId: 0x01,
                    InstrumentId,
                    PropertyId,
                    CompactPropertyValueEncoding.Boolean)
            ]);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint(
        EndpointDescriptorDefinition definition)
    {
        return new RuntimeContext()
            .AddEndpoint(
                definition.Materialize(
                    new EndpointId(
                        "arduino-uno-01")));
    }

    private static RuntimeProperty FindRuntimeProperty(
        RuntimeEndpoint runtimeEndpoint)
    {
        return runtimeEndpoint
            .FindInstrument(
                InstrumentId)!
            .FindProperty(
                PropertyId)!;
    }

    private static PropertyValue SetCachedValue(
        RuntimeProperty runtimeProperty,
        bool value)
    {
        var propertyValue =
            new PropertyValue(
                value,
                new DateTimeOffset(
                    2026,
                    7,
                    22,
                    12,
                    0,
                    0,
                    TimeSpan.Zero),
                PropertyQuality.Good);

        runtimeProperty.UpdateValue(
            propertyValue);

        return propertyValue;
    }

    private sealed class TestCompactSerialProtocolConnection
        : ICompactSerialProtocolConnection
    {
        private readonly CompactPropertyWriteStatus _writeStatus;
        private readonly CompactPropertyReadStatus _readStatus;
        private readonly ReadOnlyMemory<byte> _confirmationValue;

        public TestCompactSerialProtocolConnection(
            CompactPropertyWriteStatus writeStatus,
            CompactPropertyReadStatus readStatus,
            ReadOnlyMemory<byte> confirmationValue)
        {
            _writeStatus =
                writeStatus;

            _readStatus =
                readStatus;

            _confirmationValue =
                confirmationValue;
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

        public int ExchangeCallCount
        {
            get;
            private set;
        }

        public ReadOnlyMemory<byte> WrittenValue
        {
            get;
            private set;
        }

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ExchangeCallCount++;

            if (request.MessageType
                == (byte)CompactSerialMessageType.WritePropertyRequest)
            {
                CompactWritePropertyRequest writeRequest =
                    CompactWritePropertyCodec.DecodeRequest(
                        request);

                WrittenValue =
                    writeRequest.Value.ToArray();

                return Task.FromResult(
                    CompactWritePropertyCodec.EncodeResponse(
                        new CompactWritePropertyResponse(
                            writeRequest.CorrelationId,
                            writeRequest.PropertyId,
                            _writeStatus)));
            }

            CompactReadPropertyRequest readRequest =
                CompactReadPropertyCodec.DecodeRequest(
                    request);

            return Task.FromResult(
                CompactReadPropertyCodec.EncodeResponse(
                    new CompactReadPropertyResponse(
                        readRequest.CorrelationId,
                        readRequest.PropertyId,
                        _readStatus,
                        _confirmationValue)));
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