#pragma once

#include <Arduino.h>

#include "HaseBinaryProtocolWriter.h"
#include "HaseDescriptorModel.h"

class HasePropertyDescriptorSerializer
{
public:
    static bool Write(
        HaseBinaryProtocolWriter& writer,
        const HasePropertyDescriptor& descriptor);
};