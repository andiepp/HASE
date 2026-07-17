#pragma once

#include <Arduino.h>

#include "HasePhysicalPropertyService.h"
#include "HaseProtocolEnvelope.h"

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