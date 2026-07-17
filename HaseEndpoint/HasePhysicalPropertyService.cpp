#include "HasePhysicalPropertyService.h"

#include <cmath>
#include <cstring>

namespace
{
    using SensorReadFunction =
        float (HaseBme280Sensor::*)();

    struct PropertyReader
    {
        const char* propertyId;

        SensorReadFunction read;
    };

    const PropertyReader PropertyReaders[] =
    {
        {
            "physical.environment-sensor.temperature",
            &HaseBme280Sensor::readTemperatureCelsius
        },
        {
            "physical.environment-sensor.relative-humidity",
            &HaseBme280Sensor::readRelativeHumidity
        },
        {
            "physical.environment-sensor.air-pressure",
            &HaseBme280Sensor::readAirPressureHectopascal
        }
    };

    constexpr size_t PropertyReaderCount =
        sizeof(PropertyReaders)
        / sizeof(PropertyReaders[0]);
}

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

HasePhysicalPropertyReadResult
    HasePhysicalPropertyService::readDouble(
        const char* instrumentId,
        const char* propertyId,
        double& value)
{
    value =
        0.0;

    if (instrumentId == nullptr
        || strcmp(
               instrumentId,
               EnvironmentSensorInstrumentId)
            != 0)
    {
        return
            HasePhysicalPropertyReadResult::
                InstrumentNotFound;
    }

    if (propertyId == nullptr)
    {
        return
            HasePhysicalPropertyReadResult::
                PropertyNotFound;
    }

    for (size_t index = 0;
         index < PropertyReaderCount;
         index++)
    {
        const PropertyReader& propertyReader =
            PropertyReaders[index];

        if (strcmp(
                propertyId,
                propertyReader.propertyId)
            != 0)
        {
            continue;
        }

        float sensorValue =
            (_sensor.*propertyReader.read)();

        if (isnan(
                sensorValue))
        {
            return
                HasePhysicalPropertyReadResult::
                    SensorUnavailable;
        }

        value =
            static_cast<double>(
                sensorValue);

        return
            HasePhysicalPropertyReadResult::
                Success;
    }

    return
        HasePhysicalPropertyReadResult::
            PropertyNotFound;
}

HasePhysicalPropertyReadResult
    HasePhysicalPropertyService::readBoolean(
        const char* instrumentId,
        const char* propertyId,
        bool& value)
{
    value =
        false;

    if (instrumentId == nullptr
        || strcmp(
               instrumentId,
               ControllerInstrumentId)
            != 0)
    {
        return
            HasePhysicalPropertyReadResult::
                InstrumentNotFound;
    }

    if (propertyId == nullptr
        || strcmp(
               propertyId,
               StatusLedEnabledPropertyId)
            != 0)
    {
        return
            HasePhysicalPropertyReadResult::
                PropertyNotFound;
    }

    if (_statusLed == nullptr)
    {
        return
            HasePhysicalPropertyReadResult::
                SensorUnavailable;
    }

    if (!_statusLed->isInitialized())
    {
        _statusLed->begin();
    }

    if (!_statusLed->isInitialized())
    {
        return
            HasePhysicalPropertyReadResult::
                SensorUnavailable;
    }

    value =
        _statusLed->isEnabled();

    return
        HasePhysicalPropertyReadResult::
            Success;
}

HasePhysicalPropertyWriteResult
    HasePhysicalPropertyService::writeBoolean(
        const char* instrumentId,
        const char* propertyId,
        bool value)
{
    if (instrumentId == nullptr
        || strcmp(
               instrumentId,
               ControllerInstrumentId)
            != 0)
    {
        return
            HasePhysicalPropertyWriteResult::
                InstrumentNotFound;
    }

    if (propertyId == nullptr
        || strcmp(
               propertyId,
               StatusLedEnabledPropertyId)
            != 0)
    {
        return
            HasePhysicalPropertyWriteResult::
                PropertyNotFound;
    }

    if (_statusLed == nullptr)
    {
        return
            HasePhysicalPropertyWriteResult::
                HardwareUnavailable;
    }

    if (!_statusLed->isInitialized())
    {
        _statusLed->begin();
    }

    if (!_statusLed->isInitialized())
    {
        return
            HasePhysicalPropertyWriteResult::
                HardwareUnavailable;
    }

    _statusLed->setEnabled(
        value);

    if (_statusLed->isEnabled()
        != value)
    {
        return
            HasePhysicalPropertyWriteResult::
                HardwareUnavailable;
    }

    return
        HasePhysicalPropertyWriteResult::
            Success;
}

HasePhysicalCommandExecutionResult
    HasePhysicalPropertyService::toggleStatusLed(
        const char* instrumentId,
        const char* commandPath,
        bool& enabled)
{
    enabled =
        false;

    if (instrumentId == nullptr
        || strcmp(
               instrumentId,
               ControllerInstrumentId)
            != 0)
    {
        return
            HasePhysicalCommandExecutionResult::
                InstrumentNotFound;
    }

    if (commandPath == nullptr
        || strcmp(
               commandPath,
               ToggleStatusLedCommandPath)
            != 0)
    {
        return
            HasePhysicalCommandExecutionResult::
                CommandNotFound;
    }

    if (_statusLed == nullptr)
    {
        return
            HasePhysicalCommandExecutionResult::
                HardwareUnavailable;
    }

    if (!_statusLed->isInitialized())
    {
        _statusLed->begin();
    }

    if (!_statusLed->isInitialized())
    {
        return
            HasePhysicalCommandExecutionResult::
                HardwareUnavailable;
    }

    enabled =
        _statusLed->toggleEnabled();

    if (_statusLed->isEnabled()
        != enabled)
    {
        return
            HasePhysicalCommandExecutionResult::
                HardwareUnavailable;
    }

    return
        HasePhysicalCommandExecutionResult::
            Success;
}