#include "EndpointApplication.h"

#include <Wire.h>
#include <cmath>

bool EndpointApplication::beginHardware()
{
    Serial.println(
        "Initializing BME280 environment sensor...");

    Wire.begin(
        Bme280SdaPin,
        Bme280SclPin);

    _sensorInitialized =
        _environmentSensor.begin(
            Bme280I2cAddress,
            &Wire);

    if (!_sensorInitialized)
    {
        Serial.println(
            "BME280 initialization failed.");

        return false;
    }

    Serial.println(
        "BME280 initialized.");

    printInitialEnvironmentReading();

    return true;
}

void EndpointApplication::beginEventDetection()
{
    pinMode(
        ButtonPin,
        INPUT_PULLUP);

    const uint8_t initialLevel =
        digitalRead(
            ButtonPin);

    _buttonRawLevel =
        initialLevel;

    _buttonStableLevel =
        initialLevel;

    _buttonRawLevelChangedAt =
        millis();

    _buttonPressArmed =
        initialLevel == HIGH;

    _eventDetectionInitialized =
        true;
}

void EndpointApplication::update(
    HaseEndpointRuntime& runtime)
{
    if (!_eventDetectionInitialized)
    {
        return;
    }

    const unsigned long now =
        millis();

    const uint8_t currentRawLevel =
        digitalRead(
            ButtonPin);

    if (currentRawLevel != _buttonRawLevel)
    {
        _buttonRawLevel =
            currentRawLevel;

        _buttonRawLevelChangedAt =
            now;
    }

    if (_buttonRawLevel == _buttonStableLevel)
    {
        return;
    }

    if (now - _buttonRawLevelChangedAt
        < ButtonDebounceMilliseconds)
    {
        return;
    }

    _buttonStableLevel =
        _buttonRawLevel;

    if (_buttonStableLevel == HIGH)
    {
        _buttonPressArmed =
            true;

        return;
    }

    if (!_buttonPressArmed)
    {
        return;
    }

    _buttonPressArmed =
        false;

    if (_buttonPressedEvent != nullptr)
    {
        runtime.publishNullEvent(
            *_buttonPressedEvent);
    }
}

HaseApplicationResult EndpointApplication::readTemperature(
    double& value)
{
    return readSensor(
        &Adafruit_BME280::readTemperature,
        value);
}

HaseApplicationResult EndpointApplication::readRelativeHumidity(
    double& value)
{
    return readSensor(
        &Adafruit_BME280::readHumidity,
        value);
}

HaseApplicationResult EndpointApplication::readAirPressure(
    double& value)
{
    value =
        0.0;

    if (!_sensorInitialized)
    {
        return HaseApplicationResult::Unavailable;
    }

    const float pressurePascal =
        _environmentSensor.readPressure();

    if (std::isnan(
            pressurePascal))
    {
        return HaseApplicationResult::Unavailable;
    }

    constexpr float PascalPerHectopascal =
        100.0F;

    value =
        static_cast<double>(
            pressurePascal / PascalPerHectopascal);

    return HaseApplicationResult::Success;
}

HaseApplicationResult EndpointApplication::readStatusLedEnabled(
    bool& value)
{
    value =
        false;

    if (!ensureStatusLedInitialized())
    {
        return HaseApplicationResult::Unavailable;
    }

    value =
        _statusLedEnabled;

    return HaseApplicationResult::Success;
}

HaseApplicationResult EndpointApplication::writeStatusLedEnabled(
    bool value)
{
    if (!ensureStatusLedInitialized())
    {
        return HaseApplicationResult::Unavailable;
    }

    setStatusLedEnabled(
        value);

    if (_statusLedEnabled != value)
    {
        return HaseApplicationResult::Unavailable;
    }

    return HaseApplicationResult::Success;
}

HaseApplicationResult EndpointApplication::toggleStatusLed(
    bool& enabled)
{
    enabled =
        false;

    if (!ensureStatusLedInitialized())
    {
        return HaseApplicationResult::Unavailable;
    }

    setStatusLedEnabled(
        !_statusLedEnabled);

    enabled =
        _statusLedEnabled;

    return HaseApplicationResult::Success;
}

void EndpointApplication::bindButtonPressedEvent(
    const HaseEventRegistration& eventRegistration)
{
    _buttonPressedEvent =
        &eventRegistration;
}

HaseApplicationResult EndpointApplication::readSensor(
    SensorReadFunction read,
    double& value)
{
    value =
        0.0;

    if (!_sensorInitialized)
    {
        return HaseApplicationResult::Unavailable;
    }

    const float sensorValue =
        (_environmentSensor.*read)();

    if (std::isnan(
            sensorValue))
    {
        return HaseApplicationResult::Unavailable;
    }

    value =
        static_cast<double>(
            sensorValue);

    return HaseApplicationResult::Success;
}

bool EndpointApplication::ensureStatusLedInitialized()
{
    if (_statusLedInitialized)
    {
        return true;
    }

    pinMode(
        StatusLedPin,
        OUTPUT);

    digitalWrite(
        StatusLedPin,
        HIGH);

    _statusLedEnabled =
        false;

    _statusLedInitialized =
        true;

    return true;
}

void EndpointApplication::setStatusLedEnabled(
    bool enabled)
{
    digitalWrite(
        StatusLedPin,
        enabled
            ? LOW
            : HIGH);

    _statusLedEnabled =
        enabled;
}

void EndpointApplication::printInitialEnvironmentReading()
{
    Serial.print(
        "Temperature       : ");
    Serial.println(
        _environmentSensor.readTemperature(),
        1);

    Serial.print(
        "Relative Humidity : ");
    Serial.println(
        _environmentSensor.readHumidity(),
        1);

    Serial.print(
        "Air Pressure      : ");
    Serial.println(
        _environmentSensor.readPressure() / 100.0F,
        1);
}
