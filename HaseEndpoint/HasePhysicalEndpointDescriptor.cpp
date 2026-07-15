#include "HasePhysicalEndpointDescriptor.h"

namespace
{
    const HaseEndpointMetadata EndpointMetadata =
    {
        "Ideaspark ESP32 Environment Endpoint",
        "Physical HASE endpoint running on an Ideaspark ESP32 board."
    };

    const HaseInstrumentMetadata EnvironmentSensorMetadata =
    {
        "Bosch Sensortec",
        "BMP280",
        nullptr,
        nullptr,
        nullptr,
        "Temperature and air-pressure sensor connected to the ESP32."
    };

    const HasePropertyDescriptor EnvironmentSensorProperties[] =
    {
        {
            "physical.environment-sensor.temperature",
            "Environment.Temperature",
            "Temperature",
            "Ambient temperature.",
            HasePropertyAccessMode::Read,
            HaseDataDescriptorType::Numeric,
            {
                "temperature",
                "Temperature",
                "celsius",
                "Degree Celsius",
                "\xC2\xB0"
                "C",
                {
                    true,
                    -100.0,
                    100.0
                },
                {
                    true,
                    0.1
                }
            }
        },

        {
            "physical.environment-sensor.air-pressure",
            "Environment.AirPressure",
            "Air Pressure",
            "Ambient air pressure.",
            HasePropertyAccessMode::Read,
            HaseDataDescriptorType::Numeric,
            {
                "pressure",
                "Pressure",
                "hectopascal",
                "Hectopascal",
                "hPa",
                {
                    true,
                    300.0,
                    1100.0
                },
                {
                    true,
                    0.1
                }
            }
        }
    };

    const HaseInstrumentDescriptor Instruments[] =
    {
        {
            "environment-sensor-01",
            "BMP280 Environment Sensor",
            "environment-sensor",
            EnvironmentSensorMetadata,
            EnvironmentSensorProperties,
            2,
            0,
            0
        }
    };

    const HaseEndpointDescriptor EndpointDescriptor =
    {
        "ideaspark-esp32-01",
        EndpointMetadata,
        Instruments,
        1
    };
}

const HaseEndpointDescriptor&
    HasePhysicalEndpointDescriptor::Descriptor()
{
    return EndpointDescriptor;
}