using Hase.Core.Domain.Data;
using Hase.Core.Domain.Properties;
using Hase.Simulation.Runtime.Environment;

namespace Hase.Simulation.Runtime.Tests.Environment;

public sealed class EnvironmentSensorDescriptorFactoryTests
{
    [Fact]
    public void CreateDescriptor_CreatesExpectedInstrument()
    {
        var descriptor =
            EnvironmentSensorDescriptorFactory.CreateDescriptor();

        Assert.Equal(
            EnvironmentSensorDescriptorFactory.InstrumentId,
            descriptor.Id);

        Assert.Equal(
            "Simulated Environment Sensor",
            descriptor.Name);

        Assert.Equal(
            "environment-sensor",
            descriptor.Kind.Name);

        Assert.Equal(
            "HASE",
            descriptor.Metadata.Manufacturer);

        Assert.Equal(
            "Simulation Environment Sensor",
            descriptor.Metadata.Model);
    }

    [Fact]
    public void CreateDescriptor_CreatesThreeReadOnlyProperties()
    {
        var descriptor =
            EnvironmentSensorDescriptorFactory.CreateDescriptor();

        Assert.Equal(
            3,
            descriptor.Interface.Properties.Count);

        Assert.All(
            descriptor.Interface.Properties,
            property =>
                Assert.Equal(
                    PropertyAccessMode.Read,
                    property.AccessMode));
    }

    [Fact]
    public void CreateDescriptor_CreatesTemperatureProperty()
    {
        var descriptor =
            EnvironmentSensorDescriptorFactory.CreateDescriptor();

        var property =
            descriptor.Interface.FindProperty(
                EnvironmentSensorDescriptorFactory.TemperaturePath);

        Assert.NotNull(property);

        Assert.Equal(
            "Temperature",
            property.DisplayName);

        var data =
            Assert.IsType<NumericDataDescriptor>(
                property.Data);

        Assert.Equal(
            Quantities.Temperature,
            data.Quantity);

        Assert.Equal(
            Units.Celsius,
            data.NativeUnit);

        Assert.Equal(
            new ValueRange(-100.0, 100.0),
            data.Range);

        Assert.Equal(
            0.1,
            data.Resolution?.Value);
    }

    [Fact]
    public void CreateDescriptor_CreatesRelativeHumidityProperty()
    {
        var descriptor =
            EnvironmentSensorDescriptorFactory.CreateDescriptor();

        var property =
            descriptor.Interface.FindProperty(
                EnvironmentSensorDescriptorFactory.RelativeHumidityPath);

        Assert.NotNull(property);

        Assert.Equal(
            "Relative Humidity",
            property.DisplayName);

        var data =
            Assert.IsType<NumericDataDescriptor>(
                property.Data);

        Assert.Equal(
            Quantities.RelativeHumidity,
            data.Quantity);

        Assert.Equal(
            Units.PercentRelativeHumidity,
            data.NativeUnit);

        Assert.Equal(
            new ValueRange(0.0, 100.0),
            data.Range);

        Assert.Equal(
            0.1,
            data.Resolution?.Value);
    }

    [Fact]
    public void CreateDescriptor_CreatesAirPressureProperty()
    {
        var descriptor =
            EnvironmentSensorDescriptorFactory.CreateDescriptor();

        var property =
            descriptor.Interface.FindProperty(
                EnvironmentSensorDescriptorFactory.AirPressurePath);

        Assert.NotNull(property);

        Assert.Equal(
            "Air Pressure",
            property.DisplayName);

        var data =
            Assert.IsType<NumericDataDescriptor>(
                property.Data);

        Assert.Equal(
            Quantities.Pressure,
            data.Quantity);

        Assert.Equal(
            Units.Hectopascal,
            data.NativeUnit);

        Assert.Equal(
            new ValueRange(300.0, 1100.0),
            data.Range);

        Assert.Equal(
            0.1,
            data.Resolution?.Value);
    }

    [Fact]
    public void CreateDescriptor_UsesExpectedPropertyPaths()
    {
        Assert.Equal(
            "Environment.Temperature",
            EnvironmentSensorDescriptorFactory
                .TemperaturePath
                .ToString());

        Assert.Equal(
            "Environment.RelativeHumidity",
            EnvironmentSensorDescriptorFactory
                .RelativeHumidityPath
                .ToString());

        Assert.Equal(
            "Environment.AirPressure",
            EnvironmentSensorDescriptorFactory
                .AirPressurePath
                .ToString());
    }

    [Fact]
    public void CreateDescriptor_ReturnsIndependentDescriptorInstances()
    {
        var first =
            EnvironmentSensorDescriptorFactory.CreateDescriptor();

        var second =
            EnvironmentSensorDescriptorFactory.CreateDescriptor();

        Assert.NotSame(first, second);
        Assert.NotSame(first.Interface, second.Interface);
    }
}