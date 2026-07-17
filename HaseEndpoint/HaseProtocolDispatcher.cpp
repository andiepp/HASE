/* Protocol Dispatcher */

#include "HaseProtocolDispatcher.h"

#include "HaseBinaryProtocolReader.h"

namespace
{
    constexpr uint8_t BooleanVariantType =
        1;

    constexpr uint8_t FalseBooleanValue =
        0;

    constexpr uint8_t TrueBooleanValue =
        1;

    bool SkipRequiredString(
        HaseBinaryProtocolReader& reader)
    {
        uint16_t stringLength =
            0;

        if (!reader.readUInt16(
                stringLength))
        {
            return false;
        }

        if (stringLength == 0)
        {
            return false;
        }

        return reader.skipBytes(
            stringLength);
    }

    bool IsValidReadPropertyRequestPayload(
        const HaseProtocolEnvelope& envelope)
    {
        HaseBinaryProtocolReader reader(
            envelope.payload,
            envelope.payloadLength);

        if (!SkipRequiredString(
                reader))
        {
            return false;
        }

        if (!SkipRequiredString(
                reader))
        {
            return false;
        }

        return reader.fullyConsumed();
    }

    bool IsValidWritePropertyRequestPayload(
        const HaseProtocolEnvelope& envelope)
    {
        HaseBinaryProtocolReader reader(
            envelope.payload,
            envelope.payloadLength);

        if (!SkipRequiredString(
                reader))
        {
            return false;
        }

        if (!SkipRequiredString(
                reader))
        {
            return false;
        }

        uint8_t variantType =
            0;

        if (!reader.readByte(
                variantType)
            || variantType
                != BooleanVariantType)
        {
            return false;
        }

        uint8_t encodedValue =
            0;

        if (!reader.readByte(
                encodedValue))
        {
            return false;
        }

        if (encodedValue
                != FalseBooleanValue
            && encodedValue
                != TrueBooleanValue)
        {
            return false;
        }

        return reader.fullyConsumed();
    }

    bool IsValidReadEndpointDescriptorRequestPayload(
        const HaseProtocolEnvelope& envelope)
    {
        HaseBinaryProtocolReader reader(
            envelope.payload,
            envelope.payloadLength);

        if (!SkipRequiredString(
                reader))
        {
            return false;
        }

        return reader.fullyConsumed();
    }
}

HaseProtocolDispatchResult HaseProtocolDispatcher::Dispatch(
    const HaseProtocolEnvelope& envelope)
{
    if (envelope.majorVersion
            != SupportedMajorVersion
        || envelope.minorVersion
            != SupportedMinorVersion)
    {
        return
            HaseProtocolDispatchResult::
                UnsupportedVersion;
    }

    if (envelope.messageType
        == DiscoverRequestMessageType)
    {
        if (envelope.role
                != RequestRole
            || envelope.payloadLength
                != 0)
        {
            return
                HaseProtocolDispatchResult::
                    InvalidDiscoverRequest;
        }

        return
            HaseProtocolDispatchResult::
                DiscoverRequestRecognized;
    }

    if (envelope.messageType
        == ReadPropertyRequestMessageType)
    {
        if (envelope.role
                != RequestRole
            || !IsValidReadPropertyRequestPayload(
                envelope))
        {
            return
                HaseProtocolDispatchResult::
                    InvalidReadPropertyRequest;
        }

        return
            HaseProtocolDispatchResult::
                ReadPropertyRequestRecognized;
    }

    if (envelope.messageType
        == WritePropertyRequestMessageType)
    {
        if (envelope.role
                != RequestRole
            || !IsValidWritePropertyRequestPayload(
                envelope))
        {
            return
                HaseProtocolDispatchResult::
                    InvalidWritePropertyRequest;
        }

        return
            HaseProtocolDispatchResult::
                WritePropertyRequestRecognized;
    }

    if (envelope.messageType
        == ReadEndpointDescriptorRequestMessageType)
    {
        if (envelope.role
                != RequestRole
            || !IsValidReadEndpointDescriptorRequestPayload(
                envelope))
        {
            return
                HaseProtocolDispatchResult::
                    InvalidReadEndpointDescriptorRequest;
        }

        return
            HaseProtocolDispatchResult::
                ReadEndpointDescriptorRequestRecognized;
    }

    return
        HaseProtocolDispatchResult::
            UnsupportedMessage;
}