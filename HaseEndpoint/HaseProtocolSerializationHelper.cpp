#include "HaseProtocolSerializationHelper.h"

bool HaseProtocolSerializationHelper::WriteOptionalString(
    HaseBinaryProtocolWriter& writer,
    const char* value)
{
    if (value == nullptr)
    {
        return writer.writeByte(
            NullMarker);
    }

    if (!writer.writeByte(
            ValueMarker))
    {
        return false;
    }

    return writer.writeString(
        value);
}