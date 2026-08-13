#pragma once

#include <Arduino.h>
#include <HaseEsp32Endpoint.h>

#include "HasePhysicalPropertyService.h"

class HasePhysicalExecuteCommandHandler
{
public:
    static bool CreateResponse(
        const HaseProtocolEnvelope& envelope,
        HasePhysicalPropertyService& propertyService,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength);

private:
    HasePhysicalExecuteCommandHandler() =
        delete;
};
