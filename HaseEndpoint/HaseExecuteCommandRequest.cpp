#include "HaseExecuteCommandRequest.h"

#include <cstring>

#include "HaseBinaryProtocolReader.h"

bool HaseExecuteCommandRequestDecoder::DecodeNullArgument(
    const HaseProtocolEnvelope& envelope,
    HaseExecuteCommandRequest& request)
{
    request.instrumentId[0] =
        '\0';

    request.commandPath[0] =
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
            request.commandPath,
            sizeof(request.commandPath)))
    {
        return false;
    }

    uint8_t variantType =
        0xFF;

    if (!reader.readByte(
            variantType))
    {
        return false;
    }

    if (variantType
        != NullVariantType)
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
            request.commandPath)
        == 0)
    {
        return false;
    }

    return true;
}