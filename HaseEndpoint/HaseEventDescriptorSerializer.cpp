#include "HaseEventDescriptorSerializer.h"

#include "HaseProtocolSerializationHelper.h"

bool HaseEventDescriptorSerializer::Write(
    HaseBinaryProtocolWriter& writer,
    const HaseEventDescriptor& descriptor)
{
    if (descriptor.path == nullptr
        || descriptor.displayName == nullptr)
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

    return writer.succeeded();
}