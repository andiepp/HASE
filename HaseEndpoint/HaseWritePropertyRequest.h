#pragma once

#include <Arduino.h>

#include "HaseProtocolEnvelope.h"

struct HaseWritePropertyRequest
{
    static constexpr size_t MaximumInstrumentIdLength =
        128;

    static constexpr size_t MaximumPropertyIdLength =
        128;

    char instrumentId[
        MaximumInstrumentIdLength + 1];

    char propertyId[
        MaximumPropertyIdLength + 1];

    bool value;
};

class HaseWritePropertyRequestDecoder
{
public:
    static bool DecodeBoolean(
        const HaseProtocolEnvelope& envelope,
        HaseWritePropertyRequest& request);

private:
    static constexpr uint8_t BooleanVariantType =
        1;

    static constexpr uint8_t FalseValue =
        0;

    static constexpr uint8_t TrueValue =
        1;

    HaseWritePropertyRequestDecoder() =
        delete;
};