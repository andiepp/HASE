#include "HaseEndpointDescriptorSerializer.h"

#include "HaseEndpointMetadataSerializer.h"
#include "HaseInstrumentDescriptorSerializer.h"

bool HaseEndpointDescriptorSerializer::Write(
    HaseBinaryProtocolWriter& writer,
    const HaseEndpointDescriptor& descriptor)
{
    if (descriptor.id == nullptr)
    {
        return false;
    }

    if (descriptor.instrumentCount > 0
        && descriptor.instruments == nullptr)
    {
        return false;
    }

    if (!writer.writeString(
            descriptor.id))
    {
        return false;
    }

    if (!HaseEndpointMetadataSerializer::Write(
            writer,
            descriptor.metadata))
    {
        return false;
    }

    if (!writer.writeCount(
            descriptor.instrumentCount))
    {
        return false;
    }

    for (uint16_t index = 0;
         index < descriptor.instrumentCount;
         ++index)
    {
        if (!HaseInstrumentDescriptorSerializer::Write(
                writer,
                descriptor.instruments[index]))
        {
            return false;
        }
    }

    return writer.succeeded();
}