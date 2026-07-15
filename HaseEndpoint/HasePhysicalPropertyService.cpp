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

    if (propertyId == nullptr
        || strcmp(
               propertyId,
               TemperaturePropertyId)
            != 0)
    {
        return
            HasePhysicalPropertyReadResult::
                PropertyNotFound;
    }

    float temperature =
        _sensor.readTemperatureCelsius();

    if (isnan(
            temperature))
    {
        return
            HasePhysicalPropertyReadResult::
                SensorUnavailable;
    }

    value =
        static_cast<double>(
            temperature);

    return
        HasePhysicalPropertyReadResult::
            Success;
}