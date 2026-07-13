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

    [Fact]
    public void ResetTargetTemperature_ShouldRestoreDefaultTemperature()
    {
        // Arrange
        var state =
            new EnvironmentControllerState(35.0);

        var simulation =
            new EnvironmentControllerSimulation(state);

        // Act
        simulation.ResetTargetTemperature();

        // Assert
        Assert.Equal(
            EnvironmentControllerSimulation
                .DefaultTargetTemperature,
            state.TargetTemperature,
            precision: 10);
    }
}