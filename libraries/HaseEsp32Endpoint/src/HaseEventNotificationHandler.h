#pragma once

#include <Arduino.h>

#include "HaseEndpointDefinition.h"
#include "HaseTcpTransport.h"
#include "HaseUtcClock.h"

class HaseEventNotificationHandler
{
public:
    static bool PublishNull(
        const HaseEventRegistration& registration,
        const HaseUtcClock& utcClock,
        HaseTcpTransport& transport);

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

    static bool CreateNullFrame(
        const HaseEventRegistration& registration,
        int64_t unixTimeMilliseconds,
        uint8_t* frame,
        size_t frameCapacity,
        uint32_t& frameLength);

    HaseEventNotificationHandler() =
        delete;
};
