#pragma once

#include "HaseBinaryProtocolWriter.h"
#include "HaseDescriptorModel.h"

class HaseCommandDescriptorSerializer
{
public:
    static bool Write(
        HaseBinaryProtocolWriter& writer,
        const HaseCommandDescriptor& descriptor);
};