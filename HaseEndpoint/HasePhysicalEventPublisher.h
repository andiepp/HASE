#pragma once

#include <Arduino.h>

#include "HaseTcpTransport.h"
#include "HaseUtcClock.h"

class HasePhysicalEventPublisher
{
public:
    static constexpr uint8_t ButtonPin =
        17;

    static constexpr unsigned long DebounceMilliseconds =
        50;

    static constexpr const char* ControllerInstrumentId =
        "controller-01";

    static constexpr const char* ButtonPressedEventPath =
        "Controller.ButtonPressed";

    HasePhysicalEventPublisher(
        HaseTcpTransport& transport,
        const HaseUtcClock& utcClock);

    void begin();

    void update();

    bool publishButtonPressed();

private:
    static constexpr uint8_t ProtocolMajorVersion =
        1;

    static constexpr uint8_t ProtocolMinorVersion =
        0;

    static constexpr uint8_t NotificationRole =
        3;

    static constexpr uint8_t EventNotificationMessageType =
        40;

    static constexpr uint32_t NotificationCorrelationId =
        0;

    static constexpr uint8_t NullVariantType =
        0;

    static constexpr size_t PayloadCapacity =
        128;

    static constexpr size_t FrameCapacity =
        256;

    HaseTcpTransport& _transport;

    const HaseUtcClock& _utcClock;

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

    bool createButtonPressedFrame(
        int64_t unixTimeMilliseconds,
        uint8_t* frame,
        size_t frameCapacity,
        uint32_t& frameLength) const;
};