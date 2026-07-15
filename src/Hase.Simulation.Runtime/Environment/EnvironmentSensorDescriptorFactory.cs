using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Simulation.Runtime.Environment;

/// <summary>
/// Creates the HASE descriptor for a simulated environment sensor.
/// </summary>
public static class EnvironmentSensorDescriptorFactory
{
    public static readonly InstrumentId InstrumentId =
        new("simulation.environment-sensor");

    public static readonly DescriptorPath TemperaturePath =
        new("Environment", "Temperature");

    public static readonly DescriptorPath RelativeHumidityPath =
        new("Environment", "RelativeHumidity");

    public static readonly DescriptorPath AirPressurePath =
        new("Environment", "AirPressure");

    public static InstrumentDescriptor CreateDescriptor()
    {
        var temperature = new PropertyDescriptor(
            id: new PropertyId(
                "simulation.environment-sensor.temperature"),
            path: TemperaturePath,
            displayName: "Temperature",
            data: new NumericDataDescriptor(
                quantity: Quantities.Temperature,
                nativeUnit: Units.Celsius,
                range: new ValueRange(-100.0, 100.0),
                resolution: new Resolution(0.1)))
        {
            Description = "Ambient temperature.",
            AccessMode = PropertyAccessMode.Read
        };

        var relativeHumidity = new PropertyDescriptor(
            id: new PropertyId(
                "simulation.environment-sensor.relative-humidity"),
            path: RelativeHumidityPath,
            displayName: "Relative Humidity",
            data: new NumericDataDescriptor(
                quantity: Quantities.RelativeHumidity,
                nativeUnit: Units.PercentRelativeHumidity,
                range: new ValueRange(0.0, 100.0),
                resolution: new Resolution(0.1)))
        {
            Description = "Ambient relative humidity.",
            AccessMode = PropertyAccessMode.Read
        };

        var airPressure = new PropertyDescriptor(
            id: new PropertyId(
                "simulation.environment-sensor.air-pressure"),
            path: AirPressurePath,
            displayName: "Air Pressure",
            data: new NumericDataDescriptor(
                quantity: Quantities.Pressure,
                nativeUnit: Units.Hectopascal,
                range: new ValueRange(300.0, 1100.0),
                resolution: new Resolution(0.1)))
        {
            Description = "Ambient air pressure.",
            AccessMode = PropertyAccessMode.Read
        };

        return new InstrumentDescriptor(
            id: InstrumentId,
            name: "Simulated Environment Sensor",
            kind: new InstrumentKind("environment-sensor"))
        {
            Metadata = new InstrumentMetadata
            {
                Manufacturer = "HASE",
                Model = "Simulation Environment Sensor",
                Description =
                    "A simulated multi-value sensor for temperature, " +
                    "relative humidity, and air pressure."
            },

            Interface = new InstrumentInterface(
                properties:
                [
                    temperature,
                    relativeHumidity,
                    airPressure
                ])
        };
    }
}