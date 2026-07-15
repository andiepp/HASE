#include "HaseBme280Sensor.h"

#include <Wire.h>

bool HaseBme280Sensor::begin()
{
    _initialized =
        false;

    Wire.begin(
        SdaPin,
        SclPin);

    _initialized =
        _sensor.begin(
            I2cAddress,
            &Wire);

    return _initialized;
}

bool HaseBme280Sensor::isInitialized() const
{
    return _initialized;
}

uint32_t HaseBme280Sensor::sensorId()
{
    if (!_initialized)
    {
        return 0;
    }

    return _sensor.sensorID();
}

float HaseBme280Sensor::readTemperatureCelsius()
{
    if (!_initialized)
    {
        return NAN;
    }

    return _sensor.readTemperature();
}

float HaseBme280Sensor::readRelativeHumidity()
{
    if (!_initialized)
    {
        return NAN;
    }

    return _sensor.readHumidity();
}

float HaseBme280Sensor::readAirPressureHectopascal()
{
    if (!_initialized)
    {
        return NAN;
    }

    constexpr float PascalPerHectopascal =
        100.0F;

    return
        _sensor.readPressure()
        / PascalPerHectopascal;
}