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