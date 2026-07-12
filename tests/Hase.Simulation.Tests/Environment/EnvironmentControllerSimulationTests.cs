using Hase.Simulation.Environment;

namespace Hase.Simulation.Tests.Environment;

public sealed class EnvironmentControllerSimulationTests
{
    [Fact]
    public void SetTargetTemperature_ShouldUpdateControllerState()
    {
        // Arrange
        var state =
            new EnvironmentControllerState(20.0);

        var simulation =
            new EnvironmentControllerSimulation(state);

        // Act
        simulation.SetTargetTemperature(25.0);

        // Assert
        Assert.Equal(
            25.0,
            state.TargetTemperature,
            precision: 10);
    }
}