#include "EndpointApplication.h"

namespace
{
    const HaseEndpointMetadata EndpointMetadata =
    {
        "DOIT ESP32 DEVKITC V4 Environment Endpoint",
        "Physical HASE endpoint running on a DOIT ESP32 DEVKITC V4 board."
    };

    const HaseInstrumentMetadata EnvironmentSensorMetadata =
    {
        "Bosch Sensortec",
        "BME280",
        nullptr,
        nullptr,
        nullptr,
        "Temperature, relative-humidity, and air-pressure sensor "
        "connected to the ESP32 through I2C."
    };

    const HaseInstrumentMetadata ControllerMetadata =
    {
        "Espressif Systems",
        "ESP32",
        nullptr,
        nullptr,
        nullptr,
        "GPIO controller provided by the ESP32. "
        "The status LED output uses GPIO16 with active-low behavior. "
        "The pushbutton input uses GPIO17 with active-low behavior "
        "and the internal pull-up."
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
                "\xC2\xB0" "C",
                { true, -100.0, 100.0 },
                { true, 0.1 }
            }
        },
        {
            "physical.environment-sensor.relative-humidity",
            "Environment.RelativeHumidity",
            "Relative Humidity",
            "Ambient relative humidity.",
            HasePropertyAccessMode::Read,
            HaseDataDescriptorType::Numeric,
            {
                "relative-humidity",
                "Relative Humidity",
                "percent-relative-humidity",
                "Percent Relative Humidity",
                "%RH",
                { true, 0.0, 100.0 },
                { true, 0.1 }
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
                { true, 300.0, 1100.0 },
                { true, 0.1 }
            }
        }
    };

    const HasePropertyDescriptor ControllerProperties[] =
    {
        {
            "physical.controller.status-led-enabled",
            "Controller.StatusLedEnabled",
            "Status LED Enabled",
            "Controls the active-low status LED on GPIO16.",
            HasePropertyAccessMode::ReadWrite,
            HaseDataDescriptorType::Boolean,
            {}
        }
    };

    const HaseCommandDescriptor ControllerCommands[] =
    {
        {
            "Controller.ToggleStatusLed",
            "Toggle Status LED",
            "Toggles the active-low status LED on GPIO16 "
            "and returns its new enabled state."
        }
    };

    const HaseEventDescriptor ControllerEvents[] =
    {
        {
            "Controller.ButtonPressed",
            "Button Pressed",
            "Raised once when the active-low pushbutton on "
            "GPIO17 is debounced as pressed."
        }
    };

    const HaseInstrumentDescriptor Instruments[] =
    {
        {
            "environment-sensor-01",
            "BME280 Environment Sensor",
            "environment-sensor",
            EnvironmentSensorMetadata,
            EnvironmentSensorProperties,
            3,
            nullptr,
            0,
            nullptr,
            0
        },
        {
            "controller-01",
            "ESP32 GPIO Controller",
            "controller",
            ControllerMetadata,
            ControllerProperties,
            1,
            ControllerCommands,
            1,
            ControllerEvents,
            1
        }
    };

    const HaseEndpointDescriptor EndpointDescriptor =
    {
        "doit-esp32-devkitc-v4-01",
        EndpointMetadata,
        Instruments,
        2
    };

    HaseApplicationResult ReadTemperature(
        void* context,
        double& value)
    {
        return static_cast<EndpointApplication*>(context)->
            readTemperature(value);
    }

    HaseApplicationResult ReadRelativeHumidity(
        void* context,
        double& value)
    {
        return static_cast<EndpointApplication*>(context)->
            readRelativeHumidity(value);
    }

    HaseApplicationResult ReadAirPressure(
        void* context,
        double& value)
    {
        return static_cast<EndpointApplication*>(context)->
            readAirPressure(value);
    }

    HaseApplicationResult ReadStatusLedEnabled(
        void* context,
        bool& value)
    {
        return static_cast<EndpointApplication*>(context)->
            readStatusLedEnabled(value);
    }

    HaseApplicationResult WriteStatusLedEnabled(
        void* context,
        bool value)
    {
        return static_cast<EndpointApplication*>(context)->
            writeStatusLedEnabled(value);
    }

    HaseApplicationResult ToggleStatusLed(
        void* context,
        bool& enabled)
    {
        return static_cast<EndpointApplication*>(context)->
            toggleStatusLed(enabled);
    }
}

const HaseEndpointDefinition& CreateEndpointDefinition(
    EndpointApplication& application)
{
    static HasePropertyRegistration properties[4];
    static HaseCommandRegistration commands[1];
    static HaseEventRegistration events[1];
    static HaseEndpointDefinition definition;

    properties[0] =
    {
        &Instruments[0],
        &EnvironmentSensorProperties[0],
        &application,
        ReadTemperature,
        nullptr,
        nullptr
    };

    properties[1] =
    {
        &Instruments[0],
        &EnvironmentSensorProperties[1],
        &application,
        ReadRelativeHumidity,
        nullptr,
        nullptr
    };

    properties[2] =
    {
        &Instruments[0],
        &EnvironmentSensorProperties[2],
        &application,
        ReadAirPressure,
        nullptr,
        nullptr
    };

    properties[3] =
    {
        &Instruments[1],
        &ControllerProperties[0],
        &application,
        nullptr,
        ReadStatusLedEnabled,
        WriteStatusLedEnabled
    };

    commands[0] =
    {
        &Instruments[1],
        &ControllerCommands[0],
        &application,
        ToggleStatusLed
    };

    events[0] =
    {
        &Instruments[1],
        &ControllerEvents[0]
    };

    definition =
    {
        &EndpointDescriptor,
        properties,
        4,
        commands,
        1,
        events,
        1
    };

    application.bindButtonPressedEvent(
        events[0]);

    return definition;
}
