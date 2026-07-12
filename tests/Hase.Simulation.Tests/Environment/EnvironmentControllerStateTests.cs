using System;
using System.Collections.Generic;
using System.Text;

using Hase.Simulation.Environment;

namespace Hase.Simulation.Tests.Environment;

public sealed class EnvironmentControllerStateTests
{
    [Fact]
    public void Constructor_ShouldSetInitialTargetTemperature()
    {
        // Act
        var state =
            new EnvironmentControllerState(
                targetTemperature: 21.5);

        // Assert
        Assert.Equal(
            21.5,
            state.TargetTemperature,
            precision: 10);
    }

    [Fact]
    public void SetTargetTemperature_ShouldChangeTargetTemperature()
    {
        // Arrange
        var state =
            new EnvironmentControllerState(
                targetTemperature: 21.5);

        // Act
        state.SetTargetTemperature(
            targetTemperature: 23.0);

        // Assert
        Assert.Equal(
            23.0,
            state.TargetTemperature,
            precision: 10);
    }
}