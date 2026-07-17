#include "HaseCommandDescriptorSerializer.h"

#include "HaseProtocolSerializationHelper.h"

bool HaseCommandDescriptorSerializer::Write(
    HaseBinaryProtocolWriter& writer,
    const HaseCommandDescriptor& descriptor)
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