#pragma once

#include <Arduino.h>

#include "HaseEndpointDefinition.h"
#include "HaseProtocolDispatcher.h"
#include "HaseProtocolEnvelope.h"
#include "HaseUtcClock.h"

enum class HaseEndpointRequestProcessingResult : uint8_t
{
    NotHandled =
        0,

    ResponseCreated =
        1,

    ResponseCreationFailed =
        2
};

class HaseEndpointRequestProcessor
{
public:
    static HaseEndpointRequestProcessingResult Process(
        const HaseProtocolEnvelope& envelope,
        HaseProtocolDispatchResult dispatchResult,
        const HaseEndpointDefinition& definition,
        const HaseUtcClock& utcClock,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength);

private:
    static HaseEndpointRequestProcessingResult ProcessDiscover(
        const HaseProtocolEnvelope& envelope,
        const HaseEndpointDefinition& definition,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength);

    static HaseEndpointRequestProcessingResult ProcessReadProperty(
        const HaseProtocolEnvelope& envelope,
        const HaseEndpointDefinition& definition,
        const HaseUtcClock& utcClock,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength);

    static HaseEndpointRequestProcessingResult ProcessWriteProperty(
        const HaseProtocolEnvelope& envelope,
        const HaseEndpointDefinition& definition,
        const HaseUtcClock& utcClock,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength);

    static HaseEndpointRequestProcessingResult ProcessExecuteCommand(
        const HaseProtocolEnvelope& envelope,
        const HaseEndpointDefinition& definition,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength);

    static HaseEndpointRequestProcessingResult ProcessReadDescriptor(
        const HaseProtocolEnvelope& envelope,
        const HaseEndpointDefinition& definition,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength);

    static HaseEndpointRequestProcessingResult FromResponseCreation(
        bool responseCreated);

    HaseEndpointRequestProcessor() =
        delete;
};
