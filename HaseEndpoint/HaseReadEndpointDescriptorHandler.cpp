#include "HaseReadEndpointDescriptorHandler.h"

#include <cstring>

#include "HaseBinaryProtocolWriter.h"
#include "HaseEndpointDescriptorSerializer.h"
#include "HaseProtocolSerializationHelper.h"

namespace
{
    constexpr size_t DescriptorPayloadCapacity =
        4096;

    const char* EndpointNotFoundMessage =
        "Endpoint was not found.";

    bool RequestTargetsDescriptor(
        const HaseProtocolEnvelope& request,
        const HaseEndpointDescriptor& descriptor)
    {
        if (descriptor.id == nullptr
            || request.payload == nullptr
            || request.payloadLength < 2)
        {
            return false;
        }

        uint16_t endpointIdLength =
            static_cast<uint16_t>(
                request.payload[0])
            | static_cast<uint16_t>(
                static_cast<uint16_t>(
                    request.payload[1])
                << 8);

        size_t descriptorIdLength =
            strlen(
                descriptor.id);

        if (endpointIdLength
            != descriptorIdLength)
        {
            return false;
        }

        if (request.payloadLength
            != static_cast<uint32_t>(
                endpointIdLength)
                + 2)
        {
            return false;
        }

        return memcmp(
                   request.payload + 2,
                   descriptor.id,
                   endpointIdLength)
            == 0;
    }

    bool WriteSuccessPayload(
        HaseBinaryProtocolWriter& writer,
        const HaseEndpointDescriptor& descriptor)
    {
        if (!writer.writeByte(
                HaseReadEndpointDescriptorHandler::
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
                HaseReadEndpointDescriptorHandler::
                    DescriptorMarker))
        {
            return false;
        }

        return HaseEndpointDescriptorSerializer::Write(
            writer,
            descriptor);
    }

    bool WriteNotFoundPayload(
        HaseBinaryProtocolWriter& writer)
    {
        if (!writer.writeByte(
                HaseReadEndpointDescriptorHandler::
                    NotFoundResultCode))
        {
            return false;
        }

        if (!HaseProtocolSerializationHelper::
                WriteOptionalString(
                    writer,
                    EndpointNotFoundMessage))
        {
            return false;
        }

        return writer.writeByte(
            HaseReadEndpointDescriptorHandler::
                NoDescriptorMarker);
    }
}

bool HaseReadEndpointDescriptorHandler::CreateResponse(
    const HaseProtocolEnvelope& request,
    const HaseEndpointDescriptor& descriptor,
    uint8_t* responseFrame,
    size_t responseFrameCapacity,
    uint32_t& responseFrameLength)
{
    responseFrameLength =
        0;

    if (responseFrame == nullptr
        || descriptor.id == nullptr)
    {
        return false;
    }

    uint8_t payload[
        DescriptorPayloadCapacity];

    HaseBinaryProtocolWriter writer(
        payload,
        sizeof(payload));

    bool requestTargetsDescriptor =
        RequestTargetsDescriptor(
            request,
            descriptor);

    bool payloadWritten =
        requestTargetsDescriptor
            ? WriteSuccessPayload(
                writer,
                descriptor)
            : WriteNotFoundPayload(
                writer);

    if (!payloadWritten
        || !writer.succeeded())
    {
        return false;
    }

    HaseProtocolEnvelope response;

    response.majorVersion =
        ProtocolMajorVersion;

    response.minorVersion =
        ProtocolMinorVersion;

    response.role =
        ResponseRole;

    response.messageType =
        ReadEndpointDescriptorResponseMessageType;

    response.correlationId =
        request.correlationId;

    response.payload =
        payload;

    response.payloadLength =
        static_cast<uint32_t>(
            writer.length());

    return HaseProtocolEnvelopeCodec::Encode(
        response,
        responseFrame,
        responseFrameCapacity,
        responseFrameLength);
}