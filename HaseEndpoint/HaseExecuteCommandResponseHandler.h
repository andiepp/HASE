#pragma once

#include <Arduino.h>

#include "HaseProtocolEnvelope.h"

class HaseExecuteCommandResponseHandler
{
public:
    static constexpr uint8_t SuccessResultCode =
        0;

    static constexpr uint8_t InvalidRequestResultCode =
        1;

    static constexpr uint8_t NotFoundResultCode =
        2;

    static constexpr uint8_t NotSupportedResultCode =
        3;

    static constexpr uint8_t RejectedResultCode =
        4;

    static constexpr uint8_t InternalErrorResultCode =
        5;

    static constexpr uint8_t NullVariantType =
        0;

    static constexpr uint8_t BooleanVariantType =
        1;

    static constexpr uint8_t ProtocolMajorVersion =
        1;

    static constexpr uint8_t ProtocolMinorVersion =
        0;

    static constexpr uint8_t ResponseRole =
        2;

    static constexpr uint8_t ExecuteCommandResponseMessageType =
        31;

    static bool CreateBooleanSuccessResponse(
        const HaseProtocolEnvelope& request,
        bool returnValue,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength);

    static bool CreateFailureResponse(
        const HaseProtocolEnvelope& request,
        uint8_t resultCode,
        const char* resultMessage,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength);

private:
    HaseExecuteCommandResponseHandler() =
        delete;
};