#pragma once

#include "HaseBinaryProtocolWriter.h"
#include "HaseDescriptorModel.h"

class HaseEventDescriptorSerializer
{
public:
    static bool Write(
        HaseBinaryProtocolWriter& writer,
        const HaseEventDescriptor& descriptor);
};