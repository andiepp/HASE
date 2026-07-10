using Hase.Simulation.Values;

namespace Hase.Simulation.Tests.Values;

public sealed class ConstantValueGeneratorTests
{
    [Fact]
    public void Constructor_SetsCurrentValue()
    {
        var generator =
            new ConstantValueGenerator(42.5);

        Assert.Equal(42.5, generator.CurrentValue);
    }

    [Fact]
    public void Update_DoesNotChangeValue()
    {
        var generator =
            new ConstantValueGenerator(42.5);

        generator.Update(
            new SimulationStep(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(10)));

        Assert.Equal(42.5, generator.CurrentValue);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_NonFiniteValue_Throws(
        double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ConstantValueGenerator(value));
    }
}