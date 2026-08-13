#pragma once

#include <HaseEsp32Endpoint.h>

class HasePhysicalEndpointDescriptor
{
public:
    static const HaseEndpointDescriptor& Descriptor();

private:
    HasePhysicalEndpointDescriptor() =
        delete;
};
