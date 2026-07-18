#include "HaseInstrumentDescriptorSerializer.h"

#include "HaseCommandDescriptorSerializer.h"
#include "HaseEventDescriptorSerializer.h"
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

    if (descriptor.commandCount > 0
        && descriptor.commands == nullptr)
    {
        return false;
    }

    if (descriptor.eventCount > 0
        && descriptor.events == nullptr)
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

    for (uint16_t index = 0;
         index < descriptor.commandCount;
         ++index)
    {
        if (!HaseCommandDescriptorSerializer::Write(
                writer,
                descriptor.commands[index]))
        {
            return false;
        }
    }

    if (!writer.writeCount(
            descriptor.eventCount))
    {
        return false;
    }

    for (uint16_t index = 0;
         index < descriptor.eventCount;
         ++index)
    {
        if (!HaseEventDescriptorSerializer::Write(
                writer,
                descriptor.events[index]))
        {
            return false;
        }
    }

    return writer.succeeded();
}