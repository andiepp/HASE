#include "HaseWritePropertyRequest.h"

#include <cstring>

#include "HaseBinaryProtocolReader.h"

bool HaseWritePropertyRequestDecoder::DecodeBoolean(
    const HaseProtocolEnvelope& envelope,
    HaseWritePropertyRequest& request)
{
    request.instrumentId[0] =
        '\0';

    request.propertyId[0] =
        '\0';

    request.value =
        false;

    if (envelope.payload == nullptr
        || envelope.payloadLength == 0)
    {
        return false;
    }

    HaseBinaryProtocolReader reader(
        envelope.payload,
        envelope.payloadLength);

    if (!reader.readString(
            request.instrumentId,
            sizeof(request.instrumentId)))
    {
        return false;
    }

    if (!reader.readString(
            request.propertyId,
            sizeof(request.propertyId)))
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

    switch (encodedValue)
    {
        case FalseValue:
        {
            request.value =
                false;

            break;
        }

        case TrueValue:
        {
            request.value =
                true;

            break;
        }

        default:
        {
            return false;
        }
    }

    if (!reader.succeeded()
        || !reader.fullyConsumed())
    {
        return false;
    }

    if (strlen(
            request.instrumentId)
        == 0)
    {
        return false;
    }

    if (strlen(
            request.propertyId)
        == 0)
    {
        return false;
    }

    return true;
}