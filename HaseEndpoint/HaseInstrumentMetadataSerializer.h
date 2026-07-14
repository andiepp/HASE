#pragma once

#include <Arduino.h>

#include "HaseBinaryProtocolWriter.h"
#include "HaseDescriptorModel.h"

class HaseInstrumentMetadataSerializer
{
public:
    static bool Write(
        HaseBinaryProtocolWriter& writer,
        const HaseInstrumentMetadata& metadata);
};