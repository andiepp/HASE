using Hase.Core.Domain.Data;
using Hase.Simulation.Runtime.ByteBuffer;

namespace Hase.Simulation.Runtime.Tests.ByteBuffer;

public sealed class ByteBufferSimulationTests
{
    [Fact]
    public void Constructor_ShouldCreateEmptyBuffer()
    {
        var simulation =
            new ByteBufferSimulation();

        Assert.Equal(
            0,
            simulation.Value.Length);
    }

    [Fact]
    public void Replace_ShouldPreserveExactImmutableValue()
    {
        var replacement =
            new ByteArrayValue(
                new byte[]
                {
                    0x00,
                    0x7F,
                    0xFF
                });
        var simulation =
            new ByteBufferSimulation();

        simulation.Replace(
            replacement);

        Assert.Same(
            replacement,
            simulation.Value);
        Assert.Equal(
            replacement,
            simulation.Value);
    }

    [Fact]
    public void Replace_NullValue_ShouldThrow()
    {
        var simulation =
            new ByteBufferSimulation();

        Assert.Throws<ArgumentNullException>(
            "replacement",
            () =>
                simulation.Replace(
                    null!));
    }
}
