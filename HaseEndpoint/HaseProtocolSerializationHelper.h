#pragma once

#include <Arduino.h>

#include "HaseBinaryProtocolWriter.h"

class HaseProtocolSerializationHelper
{
public:
    static bool WriteOptionalString(
        HaseBinaryProtocolWriter& writer,
        const char* value);

private:
    static constexpr uint8_t NullMarker =
        0;

    static constexpr uint8_t ValueMarker =
        1;
};