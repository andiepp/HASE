#include "HasePhysicalPropertyService.h"

#include <cmath>
#include <cstring>

HasePhysicalPropertyService::HasePhysicalPropertyService(
    HaseBme280Sensor& sensor)
    : _sensor(
        sensor)
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

    float sensorValue =
        NAN;

    if (strcmp(
            propertyId,
            TemperaturePropertyId)
        == 0)
    {
        sensorValue =
            _sensor.readTemperatureCelsius();
    }
    else if (strcmp(
                 propertyId,
                 RelativeHumidityPropertyId)
             == 0)
    {
        sensorValue =
            _sensor.readRelativeHumidity();
    }
    else
    {
        return
            HasePhysicalPropertyReadResult::
                PropertyNotFound;
    }

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