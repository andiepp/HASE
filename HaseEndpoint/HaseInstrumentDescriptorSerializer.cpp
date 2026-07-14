#include "HaseInstrumentDescriptorSerializer.h"

#include "HaseInstrumentMetadataSerializer.h"
#include "HasePropertyDescriptorSerializer.h"

bool HaseInstrumentDescriptorSerializer::Write(
    HaseBinaryProtocolWriter& writer,
    const HaseInstrumentDescriptor& descriptor)
{
    if (descriptor.id == nullptr
        || descriptor.name == nullptr
        || descriptor.kind == nullptr)
    {
        return false;
    }

    if (descriptor.propertyCount > 0
        && descriptor.properties == nullptr)
    {
        return false;
    }

    // Command and event descriptor structures have not yet been added to the
    // ESP32 descriptor model. Until they are available, a descriptor claiming
    // to contain commands or events cannot be serialized completely.
    if (descriptor.commandCount != 0
        || descriptor.eventCount != 0)
    {
        return false;
    }

    if (!writer.writeString(
            descriptor.id))
    {
        return false;
    }

    if (!writer.writeString(
            descriptor.name))
    {
        return false;
    }

    if (!writer.writeString(
            descriptor.kind))
    {
        return false;
    }

    if (!HaseInstrumentMetadataSerializer::Write(
            writer,
            descriptor.metadata))
    {
        return false;
    }

    if (!writer.writeCount(
            descriptor.propertyCount))
    {
        return false;
    }

    for (uint16_t index = 0;
         index < descriptor.propertyCount;
         ++index)
    {
        if (!HasePropertyDescriptorSerializer::Write(
                writer,
                descriptor.properties[index]))
        {
            return false;
        }
    }

    if (!writer.writeCount(
            descriptor.commandCount))
    {
        return false;
    }

    if (!writer.writeCount(
            descriptor.eventCount))
    {
        return false;
    }

    return writer.succeeded();
}