#include "HaseReadPropertyResponseHandler.h"

#include "HaseBinaryProtocolWriter.h"
#include "HaseProtocolSerializationHelper.h"

namespace
{
    constexpr size_t ResponsePayloadCapacity =
        256;

    bool EncodeResponse(
        const HaseProtocolEnvelope& request,
        const uint8_t* payload,
        uint32_t payloadLength,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
    {
        HaseProtocolEnvelope response;

        response.majorVersion =
            HaseReadPropertyResponseHandler::
                ProtocolMajorVersion;

        response.minorVersion =
            HaseReadPropertyResponseHandler::
                ProtocolMinorVersion;

        response.role =
            HaseReadPropertyResponseHandler::
                ResponseRole;

        response.messageType =
            HaseReadPropertyResponseHandler::
                ReadPropertyResponseMessageType;

        response.correlationId =
            request.correlationId;

        response.payload =
            payload;

        response.payloadLength =
            payloadLength;

        return HaseProtocolEnvelopeCodec::Encode(
            response,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength);
    }
}

bool HaseReadPropertyResponseHandler::CreateSuccessResponse(
    const HaseProtocolEnvelope& request,
    double value,
    int64_t unixTimeMilliseconds,
    uint8_t* responseFrame,
    size_t responseFrameCapacity,
    uint32_t& responseFrameLength)
{
    responseFrameLength =
        0;

    if (responseFrame == nullptr)
    {
        return false;
    }

    uint8_t payload[
        ResponsePayloadCapacity];

    HaseBinaryProtocolWriter writer(
        payload,
        sizeof(payload));

    if (!writer.writeByte(
            SuccessResultCode))
    {
        return false;
    }

    if (!HaseProtocolSerializationHelper::
            WriteOptionalString(
                writer,
                nullptr))
    {
        return false;
    }

    if (!writer.writeByte(
            PropertyValueMarker))
    {
        return false;
    }

    if (!writer.writeByte(
            DoubleVariantType))
    {
        return false;
    }

    if (!writer.writeDouble(
            value))
    {
        return false;
    }

    if (!writer.writeInt64(
            unixTimeMilliseconds))
    {
        return false;
    }

    if (!writer.writeByte(
            GoodPropertyQuality))
    {
        return false;
    }

    if (!writer.succeeded())
    {
        return false;
    }

    return EncodeResponse(
        request,
        payload,
        static_cast<uint32_t>(
            writer.length()),
        responseFrame,
        responseFrameCapacity,
        responseFrameLength);
}

bool HaseReadPropertyResponseHandler::CreateFailureResponse(
    const HaseProtocolEnvelope& request,
    uint8_t resultCode,
    const char* resultMessage,
    uint8_t* responseFrame,
    size_t responseFrameCapacity,
    uint32_t& responseFrameLength)
{
    responseFrameLength =
        0;

    if (responseFrame == nullptr
        || resultCode == SuccessResultCode)
    {
        return false;
    }

    uint8_t payload[
        ResponsePayloadCapacity];

    HaseBinaryProtocolWriter writer(
        payload,
        sizeof(payload));

    if (!writer.writeByte(
            resultCode))
    {
        return false;
    }

    if (!HaseProtocolSerializationHelper::
            WriteOptionalString(
                writer,
                resultMessage))
    {
        return false;
    }

    if (!writer.writeByte(
            NoPropertyValueMarker))
    {
        return false;
    }

    if (!writer.succeeded())
    {
        return false;
    }

    return EncodeResponse(
        request,
        payload,
        static_cast<uint32_t>(
            writer.length()),
        responseFrame,
        responseFrameCapacity,
        responseFrameLength);
}