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

bool HaseReadPropertyResponseHandler::
    CreateDoubleSuccessResponse(
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

    if (!WriteResponsePrefix(
            writer))
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

    if (!WriteResponseSuffix(
            writer,
            unixTimeMilliseconds))
    {
        return false;
    }

    return EncodeSuccessResponse(
        request,
        writer,
        payload,
        responseFrame,
        responseFrameCapacity,
        responseFrameLength);
}

bool HaseReadPropertyResponseHandler::
    CreateBooleanSuccessResponse(
        const HaseProtocolEnvelope& request,
        bool value,
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

    if (!WriteResponsePrefix(
            writer))
    {
        return false;
    }

    if (!writer.writeByte(
            BooleanVariantType))
    {
        return false;
    }

    if (!writer.writeByte(
            value
                ? 1
                : 0))
    {
        return false;
    }

    if (!WriteResponseSuffix(
            writer,
            unixTimeMilliseconds))
    {
        return false;
    }

    return EncodeSuccessResponse(
        request,
        writer,
        payload,
        responseFrame,
        responseFrameCapacity,
        responseFrameLength);
}

bool HaseReadPropertyResponseHandler::
    CreateFailureResponse(
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

bool HaseReadPropertyResponseHandler::
    WriteResponsePrefix(
        HaseBinaryProtocolWriter& writer)
{
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

    return writer.writeByte(
        PropertyValueMarker);
}

bool HaseReadPropertyResponseHandler::
    WriteResponseSuffix(
        HaseBinaryProtocolWriter& writer,
        int64_t unixTimeMilliseconds)
{
    if (!writer.writeInt64(
            unixTimeMilliseconds))
    {
        return false;
    }

    return writer.writeByte(
        GoodPropertyQuality);
}

bool HaseReadPropertyResponseHandler::
    EncodeSuccessResponse(
        const HaseProtocolEnvelope& request,
        HaseBinaryProtocolWriter& writer,
        const uint8_t* payload,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
{
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
