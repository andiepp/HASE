#pragma once

#include "HaseDescriptorModel.h"

class HasePhysicalEndpointDescriptor
{
public:
    static const HaseEndpointDescriptor& Descriptor();

private:
    HasePhysicalEndpointDescriptor() =
        delete;
};