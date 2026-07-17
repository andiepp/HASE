#pragma once

#include <Arduino.h>

#include "HaseBme280Sensor.h"
#include "HaseStatusLed.h"

enum class HasePhysicalPropertyReadResult : uint8_t
{
    Success,

    InstrumentNotFound,

    PropertyNotFound,

    SensorUnavailable
};

enum class HasePhysicalPropertyWriteResult : uint8_t
{
    Success,

    InstrumentNotFound,

    PropertyNotFound,

    HardwareUnavailable
};

class HasePhysicalPropertyService
{
public:
    explicit HasePhysicalPropertyService(
        HaseBme280Sensor& sensor);

    HasePhysicalPropertyService(
        HaseBme280Sensor& sensor,
        HaseStatusLed& statusLed);

    HasePhysicalPropertyReadResult readDouble(
        const char* instrumentId,
        const char* propertyId,
        double& value);

    HasePhysicalPropertyReadResult readBoolean(
        const char* instrumentId,
        const char* propertyId,
        bool& value);

    HasePhysicalPropertyWriteResult writeBoolean(
        const char* instrumentId,
        const char* propertyId,
        bool value);

private:
    static constexpr const char* EnvironmentSensorInstrumentId =
        "environment-sensor-01";

    static constexpr const char* ControllerInstrumentId =
        "controller-01";

    static constexpr const char* StatusLedEnabledPropertyId =
        "physical.controller.status-led-enabled";

    HaseBme280Sensor& _sensor;

    HaseStatusLed _ownedStatusLed;

    HaseStatusLed* _statusLed;
};