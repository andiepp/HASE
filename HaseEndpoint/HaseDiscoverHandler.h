#pragma once

#include <Arduino.h>

#include "HaseProtocolEnvelope.h"

class HaseDiscoverHandler
{
public:
    static bool CreateResponse(
        const HaseProtocolEnvelope& request,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength);

    static const char* EndpointId();

    static const char* InstrumentId();

private:
    static constexpr uint8_t ProtocolMajorVersion =
        1;

    static constexpr uint8_t ProtocolMinorVersion =
        0;

    static constexpr uint8_t ResponseRole =
        2;

    static constexpr uint8_t DiscoverResponseMessageType =
        2;
};