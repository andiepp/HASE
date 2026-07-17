using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Creates the runtime descriptor expected from the physical ESP32/BME280
/// endpoint used by the physical capability scenarios.
/// </summary>
internal static class PhysicalEnvironmentEndpointDescriptorFactory
{
    public static readonly EndpointId EndpointId =
        new(
            "ideaspark-esp32-01");

    public static readonly InstrumentId InstrumentId =
        new(
            "environment-sensor-01");

    public static readonly PropertyId TemperaturePropertyId =
        new(
            "physical.environment-sensor.temperature");

    public static readonly PropertyId RelativeHumidityPropertyId =
        new(
            "physical.environment-sensor.relative-humidity");

    public static readonly PropertyId AirPressurePropertyId =
        new(
            "physical.environment-sensor.air-pressure");

    public static EndpointDescriptor Create()
    {
        var temperature =
            CreateNumericProperty(
                TemperaturePropertyId,
                new DescriptorPath(
                    "Environment",
                    "Temperature"),
                "Temperature",
                "Ambient temperature.",
                Quantities.Temperature,
                Units.Celsius,
                minimum:
                    -100.0,
                maximum:
                    100.0,
                resolution:
                    0.1);

        var relativeHumidity =
            CreateNumericProperty(
                RelativeHumidityPropertyId,
                new DescriptorPath(
                    "Environment",
                    "RelativeHumidity"),
                "Relative Humidity",
                "Ambient relative humidity.",
                Quantities.RelativeHumidity,
                Units.PercentRelativeHumidity,
                minimum:
                    0.0,
                maximum:
                    100.0,
                resolution:
                    0.1);

        var airPressure =
            CreateNumericProperty(
                AirPressurePropertyId,
                new DescriptorPath(
                    "Environment",
                    "AirPressure"),
                "Air Pressure",
                "Ambient air pressure.",
                Quantities.Pressure,
                Units.Hectopascal,
                minimum:
                    300.0,
                maximum:
                    1100.0,
                resolution:
                    0.1);

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "BME280 Environment Sensor",
                new InstrumentKind(
                    "environment-sensor"))
            {
                Metadata =
                    new InstrumentMetadata
                    {
                        Manufacturer =
                            "Bosch Sensortec",
                        Model =
                            "BME280",
                        Description =
                            "Temperature, relative-humidity, and "
                            + "air-pressure sensor connected to the "
                            + "ESP32 through I2C."
                    },
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            temperature,
                            relativeHumidity,
                            airPressure
                        ])
            };

        return new EndpointDescriptor(
            EndpointId,
            [
                instrument
            ])
        {
            Metadata =
                new EndpointMetadata
                {
                    DisplayName =
                        "DOIT ESP32 DEVKIT V4 Environment Endpoint",
                    Description =
                        "Physical HASE endpoint running on a "
                        + "DOIT ESP32 DEVKIT V4 board."
                }
        };
    }

    private static PropertyDescriptor CreateNumericProperty(
        PropertyId id,
        DescriptorPath path,
        string displayName,
        string description,
        Quantity quantity,
        Unit unit,
        double minimum,
        double maximum,
        double resolution)
    {
        return new PropertyDescriptor(
            id,
            path,
            displayName,
            new NumericDataDescriptor(
                quantity,
                unit,
                new ValueRange(
                    minimum,
                    maximum),
                new Resolution(
                    resolution)))
        {
            AccessMode =
                PropertyAccessMode.Read,
            Description =
                description
        };
    }
}