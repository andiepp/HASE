#pragma once

#include <Arduino.h>
#include <HaseEsp32Endpoint.h>

#include "HaseBme280Sensor.h"
#include "HaseStatusLed.h"

class HasePhysicalPropertyService
{
public:
    explicit HasePhysicalPropertyService(
        HaseBme280Sensor& sensor);

    HasePhysicalPropertyService(
        HaseBme280Sensor& sensor,
        HaseStatusLed& statusLed);

    HaseApplicationResult readTemperature(
        double& value);

    HaseApplicationResult readRelativeHumidity(
        double& value);

    HaseApplicationResult readAirPressure(
        double& value);

    HaseApplicationResult readStatusLedEnabled(
        bool& value);

    HaseApplicationResult writeStatusLedEnabled(
        bool value);

    HaseApplicationResult toggleStatusLed(
        bool& enabled);

private:
    using SensorReadFunction =
        float (HaseBme280Sensor::*)();

    HaseApplicationResult readSensor(
        SensorReadFunction read,
        double& value);

    bool ensureStatusLedInitialized();

    HaseBme280Sensor& _sensor;

    HaseStatusLed _ownedStatusLed;

    HaseStatusLed* _statusLed;
};
