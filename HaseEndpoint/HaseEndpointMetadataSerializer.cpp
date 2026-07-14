#include "HaseEndpointMetadataSerializer.h"

#include "HaseProtocolSerializationHelper.h"

bool HaseEndpointMetadataSerializer::Write(
    HaseBinaryProtocolWriter& writer,
    const HaseEndpointMetadata& metadata)
{
    if (!HaseProtocolSerializationHelper::
            WriteOptionalString(
                writer,
                metadata.displayName))
    {
        return false;
    }

    if (!HaseProtocolSerializationHelper::
            WriteOptionalString(
                writer,
                metadata.description))
    {
        return false;
    }

    return writer.succeeded();
}