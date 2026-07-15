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

    HaseBme280Sensor& _sensor;
};