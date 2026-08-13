#pragma once

#include <Arduino.h>
#include <HaseEsp32Endpoint.h>

class HasePhysicalEventPublisher
{
public:
    static constexpr uint8_t ButtonPin =
        17;

    static constexpr unsigned long DebounceMilliseconds =
        50;

    HasePhysicalEventPublisher(
        HaseTcpTransport& transport,
        const HaseUtcClock& utcClock,
        const HaseEventRegistration& buttonPressedEvent);

    void begin();

    void update();

    bool publishButtonPressed();

private:
    HaseTcpTransport& _transport;

    const HaseUtcClock& _utcClock;

    const HaseEventRegistration& _buttonPressedEvent;

    bool _initialized =
        false;

    uint8_t _rawLevel =
        HIGH;

    uint8_t _stableLevel =
        HIGH;

    unsigned long _rawLevelChangedAt =
        0;

    bool _pressArmed =
        true;

};
