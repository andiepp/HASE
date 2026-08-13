#include "HasePhysicalEventPublisher.h"

HasePhysicalEventPublisher::HasePhysicalEventPublisher(
    HaseTcpTransport& transport,
    const HaseUtcClock& utcClock,
    const HaseEventRegistration& buttonPressedEvent)
    : _transport(
          transport),
      _utcClock(
          utcClock),
      _buttonPressedEvent(
          buttonPressedEvent)
{
}

void HasePhysicalEventPublisher::begin()
{
    pinMode(
        ButtonPin,
        INPUT_PULLUP);

    uint8_t initialLevel =
        digitalRead(
            ButtonPin);

    _rawLevel =
        initialLevel;

    _stableLevel =
        initialLevel;

    _rawLevelChangedAt =
        millis();

    _pressArmed =
        initialLevel == HIGH;

    _initialized =
        true;
}

void HasePhysicalEventPublisher::update()
{
    if (!_initialized)
    {
        return;
    }

    unsigned long now =
        millis();

    uint8_t currentRawLevel =
        digitalRead(
            ButtonPin);

    if (currentRawLevel != _rawLevel)
    {
        _rawLevel =
            currentRawLevel;

        _rawLevelChangedAt =
            now;
    }

    if (_rawLevel == _stableLevel)
    {
        return;
    }

    if (now - _rawLevelChangedAt
        < DebounceMilliseconds)
    {
        return;
    }

    _stableLevel =
        _rawLevel;

    if (_stableLevel == HIGH)
    {
        _pressArmed =
            true;

        return;
    }

    if (!_pressArmed)
    {
        return;
    }

    _pressArmed =
        false;

    if (!_transport.hasConnectedClient())
    {
        return;
    }

    if (!publishButtonPressed()
        && _transport.hasConnectedClient())
    {
        _transport.disconnectClient();
    }
}

bool HasePhysicalEventPublisher::publishButtonPressed()
{
    return HaseEventNotificationHandler::PublishNull(
        _buttonPressedEvent,
        _utcClock,
        _transport);
}
