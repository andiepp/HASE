#pragma once

#include <Arduino.h>
#include <HaseEsp32Endpoint.h>

#include "HasePhysicalPropertyService.h"

class HasePhysicalReadPropertyHandler
{
public:
    static bool CreateResponse(
        const HaseProtocolEnvelope& envelope,
        const HaseReadPropertyRequest& request,
        HasePhysicalPropertyService& propertyService,
        HaseUtcClock& utcClock,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength);

private:
    HasePhysicalReadPropertyHandler() =
        delete;
};
