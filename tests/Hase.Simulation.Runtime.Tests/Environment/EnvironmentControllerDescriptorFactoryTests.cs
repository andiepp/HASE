using Hase.Core.Domain.Properties;
using Hase.Simulation.Runtime.Environment;

namespace Hase.Simulation.Runtime.Tests.Environment;

public sealed class EnvironmentControllerDescriptorFactoryTests
{
    [Fact]
    public void CreateDescriptor_ShouldCreateWritableTargetTemperature()
    {
        // Act
        var descriptor =
            EnvironmentControllerDescriptorFactory
                .CreateDescriptor();

        // Assert
        Assert.Equal(
            EnvironmentControllerDescriptorFactory.InstrumentId,
            descriptor.Id);

        var property =
            Assert.Single(
                descriptor.Interface.Properties);

        Assert.Equal(
            EnvironmentControllerDescriptorFactory
                .TargetTemperaturePropertyId,
            property.Id);

        Assert.Equal(
            EnvironmentControllerDescriptorFactory
                .TargetTemperaturePath,
            property.Path);

        Assert.Equal(
            PropertyAccessMode.ReadWrite,
            property.AccessMode);
    }

    [Fact]
    public void CreateDescriptor_ShouldCreateResetTargetTemperatureCommand()
    {
        // Act
        var descriptor =
            EnvironmentControllerDescriptorFactory
                .CreateDescriptor();

        // Assert
        var command =
            Assert.Single(
                descriptor.Interface.Commands);

        Assert.Equal(
            EnvironmentControllerDescriptorFactory
                .ResetTargetTemperatureCommandPath,
            command.Path);

        Assert.Equal(
            "Reset Target Temperature",
            command.DisplayName);

        Assert.Equal(
            "Resets the simulated target temperature to its default value.",
            command.Description);
    }
}