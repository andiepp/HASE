#pragma once

#include <Arduino.h>

#include "HaseBme280Sensor.h"

enum class HasePhysicalPropertyReadResult : uint8_t
{
    Success,

    InstrumentNotFound,

    PropertyNotFound,

    SensorUnavailable
};

class HasePhysicalPropertyService
{
public:
    explicit HasePhysicalPropertyService(
        HaseBme280Sensor& sensor);

    HasePhysicalPropertyReadResult readDouble(
        const char* instrumentId,
        const char* propertyId,
        double& value);

private:
    static constexpr const char* EnvironmentSensorInstrumentId =
        "environment-sensor-01";

    static constexpr const char* TemperaturePropertyId =
        "physical.environment-sensor.temperature";

    static constexpr const char* RelativeHumidityPropertyId =
        "physical.environment-sensor.relative-humidity";

    static constexpr const char* AirPressurePropertyId =
        "physical.environment-sensor.air-pressure";

    HaseBme280Sensor& _sensor;
};