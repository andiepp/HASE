#include "HaseReadPropertyRequest.h"

#include <cstring>

#include "HaseBinaryProtocolReader.h"

bool HaseReadPropertyRequestDecoder::Decode(
    const HaseProtocolEnvelope& envelope,
    HaseReadPropertyRequest& request)
{
    request.instrumentId[0] =
        '\0';

    request.propertyId[0] =
        '\0';

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