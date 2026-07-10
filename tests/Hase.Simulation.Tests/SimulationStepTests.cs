namespace Hase.Simulation.Tests;

public sealed class SimulationStepTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var elapsed = TimeSpan.FromMilliseconds(100);
        var simulationTime = TimeSpan.FromSeconds(5);

        var step = new SimulationStep(
            elapsed,
            simulationTime);

        Assert.Equal(elapsed, step.Elapsed);
        Assert.Equal(simulationTime, step.SimulationTime);
    }

    [Fact]
    public void Constructor_NegativeElapsed_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimulationStep(
                TimeSpan.FromTicks(-1),
                TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_NegativeSimulationTime_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimulationStep(
                TimeSpan.Zero,
                TimeSpan.FromTicks(-1)));
    }
}