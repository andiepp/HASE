#pragma once

#include <Arduino.h>

#include "HaseProtocolEnvelope.h"

struct HaseReadPropertyRequest
{
    static constexpr size_t MaximumInstrumentIdLength =
        128;

    static constexpr size_t MaximumPropertyIdLength =
        128;

    char instrumentId[
        MaximumInstrumentIdLength + 1];

    char propertyId[
        MaximumPropertyIdLength + 1];
};

class HaseReadPropertyRequestDecoder
{
public:
    static bool Decode(
        const HaseProtocolEnvelope& envelope,
        HaseReadPropertyRequest& request);

private:
    HaseReadPropertyRequestDecoder() =
        delete;
};