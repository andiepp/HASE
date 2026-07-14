#pragma once

#include <Arduino.h>

#include "HaseBinaryProtocolWriter.h"
#include "HaseDescriptorModel.h"

class HaseDataDescriptorSerializer
{
public:
    static bool Write(
        HaseBinaryProtocolWriter& writer,
        HaseDataDescriptorType descriptorType,
        const HaseNumericDataDescriptor& numericDescriptor);

private:
    static constexpr uint8_t NullMarker =
        0;

    static constexpr uint8_t ValueMarker =
        1;

    static bool WriteNumericDescriptor(
        HaseBinaryProtocolWriter& writer,
        const HaseNumericDataDescriptor& descriptor);

    static bool WriteOptionalRange(
        HaseBinaryProtocolWriter& writer,
        const HaseOptionalValueRange& range);

    static bool WriteOptionalResolution(
        HaseBinaryProtocolWriter& writer,
        const HaseOptionalResolution& resolution);
};