#include "HasePropertyDescriptorSerializer.h"

#include "HaseDataDescriptorSerializer.h"
#include "HaseProtocolSerializationHelper.h"

bool HasePropertyDescriptorSerializer::Write(
    HaseBinaryProtocolWriter& writer,
    const HasePropertyDescriptor& descriptor)
{
    if (descriptor.id == nullptr
        || descriptor.path == nullptr
        || descriptor.displayName == nullptr)
    {
        return false;
    }

    if (!writer.writeString(
            descriptor.id))
    {
        return false;
    }

    if (!writer.writeString(
            descriptor.path))
    {
        return false;
    }

    if (!writer.writeString(
            descriptor.displayName))
    {
        return false;
    }

    if (!HaseProtocolSerializationHelper::
            WriteOptionalString(
                writer,
                descriptor.description))
    {
        return false;
    }

    if (!writer.writeByte(
            static_cast<uint8_t>(
                descriptor.accessMode)))
    {
        return false;
    }

    if (!HaseDataDescriptorSerializer::Write(
            writer,
            descriptor.dataType,
            descriptor.numericData))
    {
        return false;
    }

    return writer.succeeded();
}