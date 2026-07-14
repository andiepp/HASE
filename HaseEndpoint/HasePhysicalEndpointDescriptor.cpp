#include "HasePhysicalEndpointDescriptor.h"

namespace
{
    const HaseEndpointMetadata EndpointMetadata =
    {
        "Ideaspark ESP32 Environment Endpoint",
        "Physical HASE endpoint running on an Ideaspark ESP32 board."
    };

    const HaseEndpointDescriptor EndpointDescriptor =
    {
        "ideaspark-esp32-01",
        EndpointMetadata,
        nullptr,
        0
    };
}

const HaseEndpointDescriptor&
    HasePhysicalEndpointDescriptor::Descriptor()
{
    return EndpointDescriptor;
}