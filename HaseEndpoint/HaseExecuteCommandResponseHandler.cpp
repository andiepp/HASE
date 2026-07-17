#include "HaseExecuteCommandResponseHandler.h"

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
            HaseExecuteCommandResponseHandler::
                ProtocolMajorVersion;

        response.minorVersion =
            HaseExecuteCommandResponseHandler::
                ProtocolMinorVersion;

        response.role =
            HaseExecuteCommandResponseHandler::
                ResponseRole;

        response.messageType =
            HaseExecuteCommandResponseHandler::
                ExecuteCommandResponseMessageType;

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

bool HaseExecuteCommandResponseHandler::
    CreateBooleanSuccessResponse(
        const HaseProtocolEnvelope& request,
        bool returnValue,
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
            BooleanVariantType))
    {
        return false;
    }

    if (!writer.writeByte(
            returnValue
                ? 1
                : 0))
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

bool HaseExecuteCommandResponseHandler::
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
            NullVariantType))
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