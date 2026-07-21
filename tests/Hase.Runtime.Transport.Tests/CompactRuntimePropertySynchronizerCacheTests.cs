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

public sealed class CompactRuntimePropertySynchronizerCacheTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly PropertyId PropertyId =
        new(
            "led-state");

    [Fact]
    public async Task SynchronizeAsync_Success_ShouldReplaceCachedValue()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        RuntimeProperty runtimeProperty =
            FindRuntimeProperty(
                runtimeEndpoint);

        var previousValue =
            new PropertyValue(
                value: true,
                new DateTimeOffset(
                    2026,
                    7,
                    21,
                    16,
                    0,
                    0,
                    TimeSpan.Zero),
                PropertyQuality.Good);

        runtimeProperty.UpdateValue(
            previousValue);

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactPropertyReadStatus.Success,
                value: new byte[]
                {
                    0x00
                });

        var synchronizer =
            new CompactRuntimePropertySynchronizer(
                connection,
                CreatePropertyMap(
                    definition));

        IReadOnlyList<
            CompactRuntimePropertySynchronizationResult> results =
            await synchronizer.SynchronizeAsync(
                runtimeEndpoint);

        CompactRuntimePropertySynchronizationResult result =
            Assert.Single(
                results);

        Assert.True(
            result.CacheUpdated);

        Assert.NotNull(
            runtimeProperty.CurrentValue);

        Assert.NotSame(
            previousValue,
            runtimeProperty.CurrentValue);

        Assert.False(
            Assert.IsType<bool>(
                runtimeProperty.CurrentValue.Value));

        Assert.True(
            runtimeProperty.CurrentValue.TimestampUtc
            > previousValue.TimestampUtc);
    }

    [Theory]
    [InlineData(
        0x01)]
    [InlineData(
        0x02)]
    public async Task SynchronizeAsync_Failure_ShouldPreserveCachedValue(
        byte statusByte)
    {
        CompactPropertyReadStatus status =
            (CompactPropertyReadStatus)statusByte;

        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                definition);

        RuntimeProperty runtimeProperty =
            FindRuntimeProperty(
                runtimeEndpoint);

        var previousValue =
            new PropertyValue(
                value: true,
                new DateTimeOffset(
                    2026,
                    7,
                    21,
                    16,
                    0,
                    0,
                    TimeSpan.Zero),
                PropertyQuality.Good);

        runtimeProperty.UpdateValue(
            previousValue);

        var connection =
            new TestCompactSerialProtocolConnection(
                status,
                ReadOnlyMemory<byte>.Empty);

        var synchronizer =
            new CompactRuntimePropertySynchronizer(
                connection,
                CreatePropertyMap(
                    definition));

        IReadOnlyList<
            CompactRuntimePropertySynchronizationResult> results =
            await synchronizer.SynchronizeAsync(
                runtimeEndpoint);

        CompactRuntimePropertySynchronizationResult result =
            Assert.Single(
                results);

        Assert.Equal(
            status,
            result.Status);

        Assert.False(
            result.CacheUpdated);

        Assert.Same(
            previousValue,
            runtimeProperty.CurrentValue);

        Assert.Equal(
            1,
            connection.ExchangeCallCount);
    }

    [Fact]
    public async Task SynchronizeAsync_MissingRuntimeInstrument_ShouldThrowBeforeExchange()
    {
        EndpointDescriptorDefinition mappedDefinition =
            CreateDefinition();

        var runtimeDefinition =
            new EndpointDescriptorDefinition();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                runtimeDefinition);

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactPropertyReadStatus.Success,
                value: new byte[]
                {
                    0x01
                });

        var synchronizer =
            new CompactRuntimePropertySynchronizer(
                connection,
                CreatePropertyMap(
                    mappedDefinition));

        async Task Act()
        {
            _ = await synchronizer.SynchronizeAsync(
                runtimeEndpoint);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);

        Assert.Equal(
            0,
            connection.ExchangeCallCount);
    }

    [Fact]
    public async Task SynchronizeAsync_MissingRuntimeProperty_ShouldThrowBeforeExchange()
    {
        EndpointDescriptorDefinition mappedDefinition =
            CreateDefinition();

        var runtimeInstrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Arduino Uno GPIO Controller",
                new InstrumentKind(
                    "controller"));

        var runtimeDefinition =
            new EndpointDescriptorDefinition(
                metadata:
                    new(),
                instruments:
                [
                    runtimeInstrument
                ]);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                runtimeDefinition);

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactPropertyReadStatus.Success,
                value: new byte[]
                {
                    0x01
                });

        var synchronizer =
            new CompactRuntimePropertySynchronizer(
                connection,
                CreatePropertyMap(
                    mappedDefinition));

        async Task Act()
        {
            _ = await synchronizer.SynchronizeAsync(
                runtimeEndpoint);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);

        Assert.Equal(
            0,
            connection.ExchangeCallCount);
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
                    PropertyAccessMode.Read
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

    private sealed class TestCompactSerialProtocolConnection
        : ICompactSerialProtocolConnection
    {
        private readonly CompactPropertyReadStatus _status;
        private readonly ReadOnlyMemory<byte> _value;

        public TestCompactSerialProtocolConnection(
            CompactPropertyReadStatus status,
            ReadOnlyMemory<byte> value)
        {
            _status =
                status;

            _value =
                value;
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

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ExchangeCallCount++;

            CompactReadPropertyRequest decodedRequest =
                CompactReadPropertyCodec.DecodeRequest(
                    request);

            return Task.FromResult(
                CompactReadPropertyCodec.EncodeResponse(
                    new CompactReadPropertyResponse(
                        decodedRequest.CorrelationId,
                        decodedRequest.PropertyId,
                        _status,
                        _value)));
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