using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Simulation.Environment;
using Hase.Simulation.Runtime.Environment;

namespace Hase.Simulation.Runtime.Tests.Environment;

public sealed class EnvironmentControllerInstrumentExecutorTests
{
    [Fact]
    public void Constructor_NullSimulation_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                new EnvironmentControllerInstrumentExecutor(
                    null!));
    }

    [Fact]
    public async Task ReadPropertyAsync_TargetTemperature_ReturnsCurrentValue()
    {
        // Arrange
        var simulation =
            CreateSimulation(
                targetTemperature: 21.5);

        var executor =
            new EnvironmentControllerInstrumentExecutor(
                simulation);

        // Act
        var result =
            await executor.ReadPropertyAsync(
                EnvironmentControllerDescriptorFactory
                    .TargetTemperaturePropertyId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Value);

        var actualValue =
            Assert.IsType<double>(
                result.Value!.Value);

        Assert.Equal(
            21.5,
            actualValue,
            precision: 10);

        Assert.Equal(
            PropertyQuality.Good,
            result.Value.Quality);

        Assert.Equal(
            TimeSpan.Zero,
            result.Value.TimestampUtc.Offset);
    }

    [Fact]
    public async Task ReadPropertyAsync_UnknownProperty_ReturnsFailure()
    {
        // Arrange
        var simulation =
            CreateSimulation(
                targetTemperature: 21.5);

        var executor =
            new EnvironmentControllerInstrumentExecutor(
                simulation);

        var unknownPropertyId =
            new PropertyId("unknown-property");

        // Act
        var result =
            await executor.ReadPropertyAsync(
                unknownPropertyId);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task WritePropertyAsync_TargetTemperature_UpdatesSimulation()
    {
        // Arrange
        var simulation =
            CreateSimulation(
                targetTemperature: 21.5);

        var executor =
            new EnvironmentControllerInstrumentExecutor(
                simulation);

        // Act
        var result =
            await executor.WritePropertyAsync(
                EnvironmentControllerDescriptorFactory
                    .TargetTemperaturePropertyId,
                value: 23.0);

        // Assert
        Assert.True(result.Success);

        Assert.Equal(
            23.0,
            simulation.State.TargetTemperature,
            precision: 10);
    }

    [Fact]
    public async Task WritePropertyAsync_UnknownProperty_ReturnsFailure()
    {
        // Arrange
        var simulation =
            CreateSimulation(
                targetTemperature: 21.5);

        var executor =
            new EnvironmentControllerInstrumentExecutor(
                simulation);

        var unknownPropertyId =
            new PropertyId("unknown-property");

        // Act
        var result =
            await executor.WritePropertyAsync(
                unknownPropertyId,
                value: 23.0);

        // Assert
        Assert.False(result.Success);

        Assert.Equal(
            21.5,
            simulation.State.TargetTemperature,
            precision: 10);
    }

    [Fact]
    public async Task WritePropertyAsync_NonDoubleValue_ReturnsFailure()
    {
        // Arrange
        var simulation =
            CreateSimulation(
                targetTemperature: 21.5);

        var executor =
            new EnvironmentControllerInstrumentExecutor(
                simulation);

        // Act
        var result =
            await executor.WritePropertyAsync(
                EnvironmentControllerDescriptorFactory
                    .TargetTemperaturePropertyId,
                value: "23.0");

        // Assert
        Assert.False(result.Success);

        Assert.Equal(
            21.5,
            simulation.State.TargetTemperature,
            precision: 10);
    }

    private static EnvironmentControllerSimulation
        CreateSimulation(
            double targetTemperature)
    {
        var state =
            new EnvironmentControllerState(
                targetTemperature);

        return new EnvironmentControllerSimulation(
            state);
    }
}