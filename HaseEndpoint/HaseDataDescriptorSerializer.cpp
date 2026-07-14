#include "HaseDataDescriptorSerializer.h"

bool HaseDataDescriptorSerializer::Write(
    HaseBinaryProtocolWriter& writer,
    HaseDataDescriptorType descriptorType,
    const HaseNumericDataDescriptor& numericDescriptor)
{
    switch (descriptorType)
    {
        case HaseDataDescriptorType::String:
        {
            return writer.writeByte(
                static_cast<uint8_t>(
                    HaseDataDescriptorType::String));
        }

        case HaseDataDescriptorType::Numeric:
        {
            return WriteNumericDescriptor(
                writer,
                numericDescriptor);
        }
    }

    return false;
}

bool HaseDataDescriptorSerializer::WriteNumericDescriptor(
    HaseBinaryProtocolWriter& writer,
    const HaseNumericDataDescriptor& descriptor)
{
    if (!writer.writeByte(
            static_cast<uint8_t>(
                HaseDataDescriptorType::Numeric)))
    {
        return false;
    }

    if (!writer.writeString(
            descriptor.quantityId))
    {
        return false;
    }

    if (!writer.writeString(
            descriptor.quantityDisplayName))
    {
        return false;
    }

    if (!writer.writeString(
            descriptor.unitId))
    {
        return false;
    }

    if (!writer.writeString(
            descriptor.unitDisplayName))
    {
        return false;
    }

    if (!writer.writeString(
            descriptor.unitSymbol))
    {
        return false;
    }

    if (!WriteOptionalRange(
            writer,
            descriptor.range))
    {
        return false;
    }

    if (!WriteOptionalResolution(
            writer,
            descriptor.resolution))
    {
        return false;
    }

    return writer.succeeded();
}

bool HaseDataDescriptorSerializer::WriteOptionalRange(
    HaseBinaryProtocolWriter& writer,
    const HaseOptionalValueRange& range)
{
    if (!range.hasValue)
    {
        return writer.writeByte(
            NullMarker);
    }

    if (!writer.writeByte(
            ValueMarker))
    {
        return false;
    }

    if (!writer.writeDouble(
            range.minimum))
    {
        return false;
    }

    return writer.writeDouble(
        range.maximum);
}

bool HaseDataDescriptorSerializer::WriteOptionalResolution(
    HaseBinaryProtocolWriter& writer,
    const HaseOptionalResolution& resolution)
{
    if (!resolution.hasValue)
    {
        return writer.writeByte(
            NullMarker);
    }

    if (!writer.writeByte(
            ValueMarker))
    {
        return false;
    }

    return writer.writeDouble(
        resolution.value);
}