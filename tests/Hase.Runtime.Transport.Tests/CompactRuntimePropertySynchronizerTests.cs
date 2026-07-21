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

public sealed class CompactRuntimePropertySynchronizerTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly PropertyId PropertyId =
        new(
            "led-state");

    [Fact]
    public async Task SynchronizeAsync_Success_ShouldPopulateRuntimeCache()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        CompactPropertyMap propertyMap =
            CreatePropertyMap(
                definition);

        RuntimeEndpoint runtimeEndpoint =
            new RuntimeContext()
                .AddEndpoint(
                    definition.Materialize(
                        new EndpointId(
                            "arduino-uno-01")));

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
                propertyMap);

        IReadOnlyList<
            CompactRuntimePropertySynchronizationResult> results =
            await synchronizer.SynchronizeAsync(
                runtimeEndpoint);

        CompactRuntimePropertySynchronizationResult result =
            Assert.Single(
                results);

        Assert.True(
            result.CacheUpdated);

        Assert.Equal(
            CompactPropertyReadStatus.Success,
            result.Status);

        RuntimeProperty runtimeProperty =
            runtimeEndpoint
                .FindInstrument(
                    InstrumentId)!
                .FindProperty(
                    PropertyId)!;

        Assert.Same(
            runtimeProperty,
            result.RuntimeProperty);

        Assert.NotNull(
            runtimeProperty.CurrentValue);

        Assert.True(
            Assert.IsType<bool>(
                runtimeProperty.CurrentValue.Value));

        Assert.Equal(
            PropertyQuality.Good,
            runtimeProperty.CurrentValue.Quality);

        Assert.Equal(
            TimeSpan.Zero,
            runtimeProperty.CurrentValue.TimestampUtc.Offset);

        Assert.Equal(
            1,
            connection.ExchangeCallCount);
    }

    [Fact]
    public async Task SynchronizeAsync_NullRuntimeEndpoint_ShouldThrow()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

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
                    definition));

        async Task Act()
        {
            _ = await synchronizer.SynchronizeAsync(
                null!);
        }

        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);

        Assert.Equal(
            0,
            connection.ExchangeCallCount);
    }

    [Fact]
    public async Task SynchronizeAsync_CancelledToken_ShouldNotRead()
    {
        EndpointDescriptorDefinition definition =
            CreateDefinition();

        RuntimeEndpoint runtimeEndpoint =
            new RuntimeContext()
                .AddEndpoint(
                    definition.Materialize(
                        new EndpointId(
                            "arduino-uno-01")));

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
                    definition));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await synchronizer.SynchronizeAsync(
                runtimeEndpoint,
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            0,
            connection.ExchangeCallCount);
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