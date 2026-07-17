#pragma once

#include <Arduino.h>

#include "HaseProtocolEnvelope.h"

struct HaseExecuteCommandRequest
{
    static constexpr size_t MaximumInstrumentIdLength =
        128;

    static constexpr size_t MaximumCommandPathLength =
        128;

    char instrumentId[
        MaximumInstrumentIdLength + 1];

    char commandPath[
        MaximumCommandPathLength + 1];
};

class HaseExecuteCommandRequestDecoder
{
public:
    static bool DecodeNullArgument(
        const HaseProtocolEnvelope& envelope,
        HaseExecuteCommandRequest& request);

private:
    static constexpr uint8_t NullVariantType =
        0;

    HaseExecuteCommandRequestDecoder() =
        delete;
};