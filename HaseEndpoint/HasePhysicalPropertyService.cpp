#include "HasePhysicalPropertyService.h"

#include <cmath>

HasePhysicalPropertyService::HasePhysicalPropertyService(
    HaseBme280Sensor& sensor)
    : _sensor(
        sensor),
      _ownedStatusLed(),
      _statusLed(
        &_ownedStatusLed)
{
}

HasePhysicalPropertyService::HasePhysicalPropertyService(
    HaseBme280Sensor& sensor,
    HaseStatusLed& statusLed)
    : _sensor(
        sensor),
      _ownedStatusLed(),
      _statusLed(
        &statusLed)
{
}

HaseApplicationResult HasePhysicalPropertyService::readTemperature(
    double& value)
{
    return readSensor(
        &HaseBme280Sensor::readTemperatureCelsius,
        value);
}

HaseApplicationResult HasePhysicalPropertyService::readRelativeHumidity(
    double& value)
{
    return readSensor(
        &HaseBme280Sensor::readRelativeHumidity,
        value);
}

HaseApplicationResult HasePhysicalPropertyService::readAirPressure(
    double& value)
{
    return readSensor(
        &HaseBme280Sensor::readAirPressureHectopascal,
        value);
}

HaseApplicationResult HasePhysicalPropertyService::readStatusLedEnabled(
    bool& value)
{
    value =
        false;

    if (!ensureStatusLedInitialized())
    {
        return HaseApplicationResult::Unavailable;
    }

    value =
        _statusLed->isEnabled();

    return HaseApplicationResult::Success;
}

HaseApplicationResult HasePhysicalPropertyService::writeStatusLedEnabled(
    bool value)
{
    if (!ensureStatusLedInitialized())
    {
        return HaseApplicationResult::Unavailable;
    }

    _statusLed->setEnabled(
        value);

    if (_statusLed->isEnabled() != value)
    {
        return HaseApplicationResult::Unavailable;
    }

    return HaseApplicationResult::Success;
}

HaseApplicationResult HasePhysicalPropertyService::toggleStatusLed(
    bool& enabled)
{
    enabled =
        false;

    if (!ensureStatusLedInitialized())
    {
        return HaseApplicationResult::Unavailable;
    }

    enabled =
        _statusLed->toggleEnabled();

    if (_statusLed->isEnabled() != enabled)
    {
        return HaseApplicationResult::Unavailable;
    }

    return HaseApplicationResult::Success;
}

HaseApplicationResult HasePhysicalPropertyService::readSensor(
    SensorReadFunction read,
    double& value)
{
    value =
        0.0;

    const float sensorValue =
        (_sensor.*read)();

    if (std::isnan(sensorValue))
    {
        return HaseApplicationResult::Unavailable;
    }

    value =
        static_cast<double>(sensorValue);

    return HaseApplicationResult::Success;
}

bool HasePhysicalPropertyService::ensureStatusLedInitialized()
{
    if (_statusLed == nullptr)
    {
        return false;
    }

    if (!_statusLed->isInitialized())
    {
        _statusLed->begin();
    }

    return _statusLed->isInitialized();
}
