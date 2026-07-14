#include "HaseInstrumentMetadataSerializer.h"

#include "HaseProtocolSerializationHelper.h"

bool HaseInstrumentMetadataSerializer::Write(
    HaseBinaryProtocolWriter& writer,
    const HaseInstrumentMetadata& metadata)
{
    if (!HaseProtocolSerializationHelper::
            WriteOptionalString(
                writer,
                metadata.manufacturer))
    {
        return false;
    }

    if (!HaseProtocolSerializationHelper::
            WriteOptionalString(
                writer,
                metadata.model))
    {
        return false;
    }

    if (!HaseProtocolSerializationHelper::
            WriteOptionalString(
                writer,
                metadata.serialNumber))
    {
        return false;
    }

    if (!HaseProtocolSerializationHelper::
            WriteOptionalString(
                writer,
                metadata.firmwareVersion))
    {
        return false;
    }

    if (!HaseProtocolSerializationHelper::
            WriteOptionalString(
                writer,
                metadata.hardwareRevision))
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