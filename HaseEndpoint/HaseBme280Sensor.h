#pragma once

#include <Arduino.h>
#include <Adafruit_BME280.h>

class HaseBme280Sensor
{
public:
    bool begin();

    bool isInitialized() const;

    uint32_t sensorId();

    float readTemperatureCelsius();

    float readRelativeHumidity();

    float readAirPressureHectopascal();

private:
    static constexpr int SdaPin =
        21;

    static constexpr int SclPin =
        22;

    static constexpr uint8_t I2cAddress =
        0x76;

    Adafruit_BME280 _sensor;

    bool _initialized =
        false;
};