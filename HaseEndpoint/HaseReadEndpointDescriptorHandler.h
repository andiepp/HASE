#pragma once

#include <Arduino.h>

#include "HaseDescriptorModel.h"
#include "HaseProtocolEnvelope.h"

class HaseReadEndpointDescriptorHandler
{
public:
    static constexpr uint8_t SuccessResultCode =
        0;

    static constexpr uint8_t NotFoundResultCode =
        2;

    static constexpr uint8_t NoDescriptorMarker =
        0;

    static constexpr uint8_t DescriptorMarker =
        1;

    static bool CreateResponse(
        const HaseProtocolEnvelope& request,
        const HaseEndpointDescriptor& descriptor,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength);

private:
    static constexpr uint8_t ProtocolMajorVersion =
        1;

    static constexpr uint8_t ProtocolMinorVersion =
        0;

    static constexpr uint8_t ResponseRole =
        2;

    static constexpr uint8_t ReadEndpointDescriptorResponseMessageType =
        53;
};