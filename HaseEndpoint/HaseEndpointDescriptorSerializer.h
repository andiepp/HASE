#pragma once

#include <Arduino.h>

#include "HaseBinaryProtocolWriter.h"
#include "HaseDescriptorModel.h"

class HaseEndpointDescriptorSerializer
{
public:
    static bool Write(
        HaseBinaryProtocolWriter& writer,
        const HaseEndpointDescriptor& descriptor);
};