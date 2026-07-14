#pragma once

#include <Arduino.h>

#include "HaseBinaryProtocolWriter.h"
#include "HaseDescriptorModel.h"

class HaseInstrumentDescriptorSerializer
{
public:
    static bool Write(
        HaseBinaryProtocolWriter& writer,
        const HaseInstrumentDescriptor& descriptor);
};