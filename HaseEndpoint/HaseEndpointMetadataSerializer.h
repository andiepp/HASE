#pragma once

#include <Arduino.h>

#include "HaseBinaryProtocolWriter.h"
#include "HaseDescriptorModel.h"

class HaseEndpointMetadataSerializer
{
public:
    static bool Write(
        HaseBinaryProtocolWriter& writer,
        const HaseEndpointMetadata& metadata);
};