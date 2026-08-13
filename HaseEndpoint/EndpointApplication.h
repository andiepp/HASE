#pragma once

#include <Adafruit_BME280.h>
#include <Arduino.h>
#include <HaseEsp32Endpoint.h>

class EndpointApplication final : public HaseEndpointApplication
{
public:
    bool beginHardware() override;

    void beginEventDetection() override;

    void update(
        HaseEndpointRuntime& runtime) override;

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

    void bindButtonPressedEvent(
        const HaseEventRegistration& eventRegistration);

private:
    static constexpr int Bme280SdaPin =
        21;

    static constexpr int Bme280SclPin =
        22;

    static constexpr uint8_t Bme280I2cAddress =
        0x76;

    static constexpr uint8_t StatusLedPin =
        16;

    static constexpr uint8_t ButtonPin =
        17;

    static constexpr unsigned long ButtonDebounceMilliseconds =
        50;

    Adafruit_BME280 _environmentSensor;

    bool _sensorInitialized =
        false;

    bool _statusLedInitialized =
        false;

    bool _statusLedEnabled =
        false;

    const HaseEventRegistration* _buttonPressedEvent =
        nullptr;

    bool _eventDetectionInitialized =
        false;

    uint8_t _buttonRawLevel =
        HIGH;

    uint8_t _buttonStableLevel =
        HIGH;

    unsigned long _buttonRawLevelChangedAt =
        0;

    bool _buttonPressArmed =
        true;

    using SensorReadFunction =
        float (Adafruit_BME280::*)();

    HaseApplicationResult readSensor(
        SensorReadFunction read,
        double& value);

    bool ensureStatusLedInitialized();

    void setStatusLedEnabled(
        bool enabled);

    void printInitialEnvironmentReading();
};

const HaseEndpointDefinition& CreateEndpointDefinition(
    EndpointApplication& application);
