#pragma once

#include <Arduino.h>

#include "HaseProtocolEnvelope.h"

class HaseReadPropertyResponseHandler
{
public:
    static constexpr uint8_t SuccessResultCode =
        0;

    static constexpr uint8_t InvalidRequestResultCode =
        1;

    static constexpr uint8_t NotFoundResultCode =
        2;

    static constexpr uint8_t InternalErrorResultCode =
        5;

    static constexpr uint8_t NoPropertyValueMarker =
        0;

    static constexpr uint8_t PropertyValueMarker =
        1;

    static constexpr uint8_t GoodPropertyQuality =
        0;

    static constexpr uint8_t ProtocolMajorVersion =
        1;

    static constexpr uint8_t ProtocolMinorVersion =
        0;

    static constexpr uint8_t ResponseRole =
        2;

    static constexpr uint8_t ReadPropertyResponseMessageType =
        11;

    static bool CreateSuccessResponse(
        const HaseProtocolEnvelope& request,
        double value,
        int64_t unixTimeMilliseconds,
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
    static constexpr uint8_t DoubleVariantType =
        4;

    HaseReadPropertyResponseHandler() =
        delete;
};