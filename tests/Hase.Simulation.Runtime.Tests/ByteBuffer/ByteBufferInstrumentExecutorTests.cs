using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Simulation.Runtime.ByteBuffer;

namespace Hase.Simulation.Runtime.Tests.ByteBuffer;

public sealed class ByteBufferInstrumentExecutorTests
{
    private static readonly DateTimeOffset Timestamp =
        new(
            2026,
            7,
            28,
            20,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Constructor_NullSimulation_ShouldThrow()
    {
        RuntimeInstrument instrument =
            CreateRuntimeInstrument();

        Assert.Throws<ArgumentNullException>(
            "simulation",
            () =>
                new ByteBufferInstrumentExecutor(
                    null!,
                    instrument));
    }

    [Fact]
    public void Constructor_NullRuntimeInstrument_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "runtimeInstrument",
            () =>
                new ByteBufferInstrumentExecutor(
                    new ByteBufferSimulation(),
                    null!));
    }

    [Fact]
    public async Task ReadPropertyAsync_Value_ShouldReturnCurrentBuffer()
    {
        var simulation =
            new ByteBufferSimulation();
        var expected =
            new ByteArrayValue(
                new byte[]
                {
                    0x01,
                    0x02
                });
        simulation.Replace(
            expected);
        var executor =
            new ByteBufferInstrumentExecutor(
                simulation,
                CreateRuntimeInstrument(),
                new FixedTimeProvider(
                    Timestamp));

        var result =
            await executor.ReadPropertyAsync(
                ByteBufferDescriptorFactory.ValuePropertyId);

        Assert.True(
            result.Success);
        Assert.NotNull(
            result.Value);
        Assert.Same(
            expected,
            result.Value.Value);
        Assert.Equal(
            Timestamp,
            result.Value.TimestampUtc);
        Assert.Equal(
            PropertyQuality.Good,
            result.Value.Quality);
    }

    [Fact]
    public async Task WritePropertyAsync_Value_ShouldUpdateAuthoritativeBuffer()
    {
        var executor =
            new ByteBufferInstrumentExecutor(
                new ByteBufferSimulation(),
                CreateRuntimeInstrument());

        var result =
            await executor.WritePropertyAsync(
                ByteBufferDescriptorFactory.ValuePropertyId,
                new ByteArrayValue(
                    new byte[]
                    {
                        0x01
                    }));

        Assert.True(
            result.Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WritePropertyAsync_Enabled_ShouldAcceptBoolean(
        bool requested)
    {
        var simulation =
            new ByteBufferSimulation();
        var executor =
            new ByteBufferInstrumentExecutor(
                simulation,
                CreateRuntimeInstrument());

        var result =
            await executor.WritePropertyAsync(
                ByteBufferDescriptorFactory.EnabledPropertyId,
                requested);

        Assert.True(
            result.Success);
        Assert.Equal(
            requested,
            simulation.Enabled);
    }

    [Theory]
    [InlineData(-40.0, true)]
    [InlineData(125.0, true)]
    [InlineData(-40.1, false)]
    [InlineData(125.1, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    public async Task WritePropertyAsync_Setpoint_ShouldEnforceFiniteRange(
        double requested,
        bool expectedSuccess)
    {
        var simulation =
            new ByteBufferSimulation();
        var executor =
            new ByteBufferInstrumentExecutor(
                simulation,
                CreateRuntimeInstrument());

        var result =
            await executor.WritePropertyAsync(
                ByteBufferDescriptorFactory.SetpointPropertyId,
                requested);

        Assert.Equal(
            expectedSuccess,
            result.Success);
        Assert.Equal(
            expectedSuccess
                ? requested
                : 20.0,
            simulation.Setpoint);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("HASE validation")]
    public async Task WritePropertyAsync_Label_ShouldPreserveExactString(
        string requested)
    {
        var simulation =
            new ByteBufferSimulation();
        var executor =
            new ByteBufferInstrumentExecutor(
                simulation,
                CreateRuntimeInstrument());

        var result =
            await executor.WritePropertyAsync(
                ByteBufferDescriptorFactory.LabelPropertyId,
                requested);

        Assert.True(
            result.Success);
        Assert.Equal(
            requested,
            simulation.Label);
    }

    [Fact]
    public async Task WritePropertyAsync_WrongType_ShouldLeaveStateUnchanged()
    {
        var simulation =
            new ByteBufferSimulation();
        var executor =
            new ByteBufferInstrumentExecutor(
                simulation,
                CreateRuntimeInstrument());

        var result =
            await executor.WritePropertyAsync(
                ByteBufferDescriptorFactory.EnabledPropertyId,
                "true");

        Assert.False(
            result.Success);
        Assert.False(
            simulation.Enabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteCommandAsync_InvalidRequest_ShouldFail(
        bool useUnknownPath)
    {
        var simulation =
            new ByteBufferSimulation();
        var executor =
            new ByteBufferInstrumentExecutor(
                simulation,
                CreateRuntimeInstrument());

        var result =
            await executor.ExecuteCommandAsync(
                useUnknownPath
                    ? new DescriptorPath(
                        "Buffer",
                        "Unknown")
                    : ByteBufferDescriptorFactory.ReplaceCommandPath,
                useUnknownPath
                    ? new ByteArrayValue(
                        new byte[]
                        {
                            0x01
                        })
                    : "01");

        Assert.False(
            result.Success);
        Assert.Null(
            result.Value);
        Assert.Equal(
            0,
            simulation.Value.Length);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Replace_ShouldUpdateAndNotifyExactlyOnce()
    {
        RuntimeInstrument instrument =
            CreateRuntimeInstrument();
        var observer =
            new RecordingObserver();
        instrument.Subscribe(
            observer);
        var simulation =
            new ByteBufferSimulation();
        var executor =
            new ByteBufferInstrumentExecutor(
                simulation,
                instrument,
                new FixedTimeProvider(
                    Timestamp));
        var replacement =
            new ByteArrayValue(
                new byte[]
                {
                    0x00,
                    0x7F,
                    0xFF
                });

        var result =
            await executor.ExecuteCommandAsync(
                ByteBufferDescriptorFactory.ReplaceCommandPath,
                replacement);

        Assert.True(
            result.Success);
        Assert.Same(
            replacement,
            result.Value);
        Assert.Same(
            replacement,
            simulation.Value);

        RuntimeProperty property =
            instrument.FindProperty(
                ByteBufferDescriptorFactory.ValuePropertyId)!;
        Assert.NotNull(
            property.CurrentValue);
        Assert.Same(
            replacement,
            property.CurrentValue.Value);
        Assert.Equal(
            Timestamp,
            property.CurrentValue.TimestampUtc);
        Assert.Equal(
            1,
            observer.NotificationCount);
    }

    private static RuntimeInstrument CreateRuntimeInstrument()
    {
        var descriptor =
            ByteBufferDescriptorFactory.CreateDescriptor();
        var context =
            new RuntimeContext();
        RuntimeEndpoint endpoint =
            context.AddEndpoint(
                new EndpointDescriptor(
                    new EndpointId(
                        "simulation-byte-buffer"),
                    [
                        descriptor
                    ]));

        return endpoint.FindInstrument(
            descriptor.Id)!;
    }

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset timestamp;

        public FixedTimeProvider(
            DateTimeOffset timestamp)
        {
            this.timestamp =
                timestamp;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return timestamp;
        }
    }

    private sealed class RecordingObserver
        : IPropertyValueObserver
    {
        public int NotificationCount
        {
            get;
            private set;
        }

        public void OnPropertyValueChanged(
            PropertyValueChanged change)
        {
            NotificationCount++;
        }
    }
}
