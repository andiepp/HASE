#include "HaseEndpointRequestProcessor.h"

#include "HaseBinaryProtocolWriter.h"
#include "HaseEndpointDefinitionResolver.h"
#include "HaseExecuteCommandRequest.h"
#include "HaseExecuteCommandResponseHandler.h"
#include "HaseReadEndpointDescriptorHandler.h"
#include "HaseReadPropertyRequest.h"
#include "HaseReadPropertyResponseHandler.h"
#include "HaseWritePropertyRequest.h"
#include "HaseWritePropertyResponseHandler.h"

namespace
{
    constexpr size_t DiscoveryPayloadCapacity =
        192;

    constexpr uint8_t ProtocolMajorVersion =
        1;

    constexpr uint8_t ProtocolMinorVersion =
        0;

    constexpr uint8_t ResponseRole =
        2;

    constexpr uint8_t DiscoverResponseMessageType =
        2;

    bool CreateDiscoveryResponse(
        const HaseProtocolEnvelope& request,
        const HaseEndpointDescriptor& descriptor,
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
            DiscoveryPayloadCapacity];

        HaseBinaryProtocolWriter writer(
            payload,
            sizeof(payload));

        if (!writer.writeString(descriptor.id)
            || !writer.writeCount(descriptor.instrumentCount))
        {
            return false;
        }

        for (uint16_t index = 0;
            index < descriptor.instrumentCount;
            index++)
        {
            if (!writer.writeString(descriptor.instruments[index].id))
            {
                return false;
            }
        }

        if (!writer.succeeded())
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
            DiscoverResponseMessageType;

        response.correlationId =
            request.correlationId;

        response.payload =
            payload;

        response.payloadLength =
            static_cast<uint32_t>(writer.length());

        return HaseProtocolEnvelopeCodec::Encode(
            response,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength);
    }

    bool CreateReadFailure(
        const HaseProtocolEnvelope& envelope,
        uint8_t resultCode,
        const char* message,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
    {
        return HaseReadPropertyResponseHandler::CreateFailureResponse(
            envelope,
            resultCode,
            message,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength);
    }

    bool CreateWriteFailure(
        const HaseProtocolEnvelope& envelope,
        uint8_t resultCode,
        const char* message,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
    {
        return HaseWritePropertyResponseHandler::CreateFailureResponse(
            envelope,
            resultCode,
            message,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength);
    }

    bool CreateCommandFailure(
        const HaseProtocolEnvelope& envelope,
        uint8_t resultCode,
        const char* message,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
    {
        return HaseExecuteCommandResponseHandler::CreateFailureResponse(
            envelope,
            resultCode,
            message,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength);
    }
}

HaseEndpointRequestProcessingResult HaseEndpointRequestProcessor::Process(
    const HaseProtocolEnvelope& envelope,
    HaseProtocolDispatchResult dispatchResult,
    const HaseEndpointDefinition& definition,
    const HaseUtcClock& utcClock,
    uint8_t* responseFrame,
    size_t responseFrameCapacity,
    uint32_t& responseFrameLength)
{
    responseFrameLength =
        0;

    switch (dispatchResult)
    {
        case HaseProtocolDispatchResult::DiscoverRequestRecognized:
        {
            return ProcessDiscover(
                envelope,
                definition,
                responseFrame,
                responseFrameCapacity,
                responseFrameLength);
        }

        case HaseProtocolDispatchResult::ReadPropertyRequestRecognized:
        {
            return ProcessReadProperty(
                envelope,
                definition,
                utcClock,
                responseFrame,
                responseFrameCapacity,
                responseFrameLength);
        }

        case HaseProtocolDispatchResult::WritePropertyRequestRecognized:

        case HaseProtocolDispatchResult::InvalidWritePropertyRequest:
        {
            return ProcessWriteProperty(
                envelope,
                definition,
                utcClock,
                responseFrame,
                responseFrameCapacity,
                responseFrameLength);
        }

        case HaseProtocolDispatchResult::ExecuteCommandRequestRecognized:

        case HaseProtocolDispatchResult::InvalidExecuteCommandRequest:
        {
            return ProcessExecuteCommand(
                envelope,
                definition,
                responseFrame,
                responseFrameCapacity,
                responseFrameLength);
        }

        case HaseProtocolDispatchResult::ReadEndpointDescriptorRequestRecognized:
        {
            return ProcessReadDescriptor(
                envelope,
                definition,
                responseFrame,
                responseFrameCapacity,
                responseFrameLength);
        }

        default:
        {
            return HaseEndpointRequestProcessingResult::NotHandled;
        }
    }
}

HaseEndpointRequestProcessingResult
    HaseEndpointRequestProcessor::ProcessDiscover(
        const HaseProtocolEnvelope& envelope,
        const HaseEndpointDefinition& definition,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
{
    if (definition.descriptor == nullptr)
    {
        return HaseEndpointRequestProcessingResult::ResponseCreationFailed;
    }

    return FromResponseCreation(
        CreateDiscoveryResponse(
            envelope,
            *definition.descriptor,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength));
}

HaseEndpointRequestProcessingResult
    HaseEndpointRequestProcessor::ProcessReadProperty(
        const HaseProtocolEnvelope& envelope,
        const HaseEndpointDefinition& definition,
        const HaseUtcClock& utcClock,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
{
    HaseReadPropertyRequest request;

    if (!HaseReadPropertyRequestDecoder::Decode(envelope, request))
    {
        return FromResponseCreation(
            CreateReadFailure(
                envelope,
                HaseReadPropertyResponseHandler::InvalidRequestResultCode,
                "Invalid request",
                responseFrame,
                responseFrameCapacity,
                responseFrameLength));
    }

    const HasePropertyRegistration* registration =
        HaseEndpointDefinitionResolver::ResolveProperty(
            definition,
            request.instrumentId,
            request.propertyId);

    if (registration == nullptr)
    {
        return FromResponseCreation(
            CreateReadFailure(
                envelope,
                HaseReadPropertyResponseHandler::NotFoundResultCode,
                "Property not found",
                responseFrame,
                responseFrameCapacity,
                responseFrameLength));
    }

    int64_t timestamp =
        0;

    if (registration->property->dataType == HaseDataDescriptorType::Numeric
        && registration->readNumeric != nullptr)
    {
        double value =
            0.0;

        const HaseApplicationResult applicationResult =
            registration->readNumeric(registration->context, value);

        if (applicationResult == HaseApplicationResult::Unavailable)
        {
            return FromResponseCreation(
                CreateReadFailure(
                    envelope,
                    HaseReadPropertyResponseHandler::InternalErrorResultCode,
                    "Property unavailable",
                    responseFrame,
                    responseFrameCapacity,
                    responseFrameLength));
        }

        if (applicationResult != HaseApplicationResult::Success)
        {
            return HaseEndpointRequestProcessingResult::ResponseCreationFailed;
        }

        if (!utcClock.tryGetUnixTimeMilliseconds(timestamp))
        {
            return FromResponseCreation(
                CreateReadFailure(
                    envelope,
                    HaseReadPropertyResponseHandler::InternalErrorResultCode,
                    "UTC clock unavailable",
                    responseFrame,
                    responseFrameCapacity,
                    responseFrameLength));
        }

        return FromResponseCreation(
            HaseReadPropertyResponseHandler::CreateDoubleSuccessResponse(
                envelope,
                value,
                timestamp,
                responseFrame,
                responseFrameCapacity,
                responseFrameLength));
    }

    if (registration->property->dataType == HaseDataDescriptorType::Boolean
        && registration->readBoolean != nullptr)
    {
        bool value =
            false;

        const HaseApplicationResult applicationResult =
            registration->readBoolean(registration->context, value);

        if (applicationResult == HaseApplicationResult::Unavailable)
        {
            return FromResponseCreation(
                CreateReadFailure(
                    envelope,
                    HaseReadPropertyResponseHandler::InternalErrorResultCode,
                    "Property unavailable",
                    responseFrame,
                    responseFrameCapacity,
                    responseFrameLength));
        }

        if (applicationResult != HaseApplicationResult::Success)
        {
            return HaseEndpointRequestProcessingResult::ResponseCreationFailed;
        }

        if (!utcClock.tryGetUnixTimeMilliseconds(timestamp))
        {
            return FromResponseCreation(
                CreateReadFailure(
                    envelope,
                    HaseReadPropertyResponseHandler::InternalErrorResultCode,
                    "UTC clock unavailable",
                    responseFrame,
                    responseFrameCapacity,
                    responseFrameLength));
        }

        return FromResponseCreation(
            HaseReadPropertyResponseHandler::CreateBooleanSuccessResponse(
                envelope,
                value,
                timestamp,
                responseFrame,
                responseFrameCapacity,
                responseFrameLength));
    }

    return FromResponseCreation(
        CreateReadFailure(
            envelope,
            HaseReadPropertyResponseHandler::NotFoundResultCode,
            "Property not found",
            responseFrame,
            responseFrameCapacity,
            responseFrameLength));
}

HaseEndpointRequestProcessingResult
    HaseEndpointRequestProcessor::ProcessWriteProperty(
        const HaseProtocolEnvelope& envelope,
        const HaseEndpointDefinition& definition,
        const HaseUtcClock& utcClock,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
{
    HaseWritePropertyRequest request;

    if (!HaseWritePropertyRequestDecoder::DecodeBoolean(envelope, request))
    {
        return FromResponseCreation(
            CreateWriteFailure(
                envelope,
                HaseWritePropertyResponseHandler::InvalidRequestResultCode,
                "Invalid request",
                responseFrame,
                responseFrameCapacity,
                responseFrameLength));
    }

    const HasePropertyRegistration* registration =
        HaseEndpointDefinitionResolver::ResolveProperty(
            definition,
            request.instrumentId,
            request.propertyId);

    if (registration == nullptr
        || registration->property->dataType != HaseDataDescriptorType::Boolean
        || registration->writeBoolean == nullptr)
    {
        return FromResponseCreation(
            CreateWriteFailure(
                envelope,
                HaseWritePropertyResponseHandler::NotFoundResultCode,
                "Property not found",
                responseFrame,
                responseFrameCapacity,
                responseFrameLength));
    }

    const HaseApplicationResult applicationResult =
        registration->writeBoolean(
            registration->context,
            request.value);

    if (applicationResult == HaseApplicationResult::Unavailable)
    {
        return FromResponseCreation(
            CreateWriteFailure(
                envelope,
                HaseWritePropertyResponseHandler::InternalErrorResultCode,
                "Property hardware unavailable",
                responseFrame,
                responseFrameCapacity,
                responseFrameLength));
    }

    if (applicationResult != HaseApplicationResult::Success)
    {
        return HaseEndpointRequestProcessingResult::ResponseCreationFailed;
    }

    int64_t timestamp =
        0;

    if (!utcClock.tryGetUnixTimeMilliseconds(timestamp))
    {
        return FromResponseCreation(
            CreateWriteFailure(
                envelope,
                HaseWritePropertyResponseHandler::InternalErrorResultCode,
                "UTC clock unavailable",
                responseFrame,
                responseFrameCapacity,
                responseFrameLength));
    }

    return FromResponseCreation(
        HaseWritePropertyResponseHandler::CreateBooleanSuccessResponse(
            envelope,
            request.value,
            timestamp,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength));
}

HaseEndpointRequestProcessingResult
    HaseEndpointRequestProcessor::ProcessExecuteCommand(
        const HaseProtocolEnvelope& envelope,
        const HaseEndpointDefinition& definition,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
{
    HaseExecuteCommandRequest request;

    if (!HaseExecuteCommandRequestDecoder::DecodeNullArgument(
            envelope,
            request))
    {
        return FromResponseCreation(
            CreateCommandFailure(
                envelope,
                HaseExecuteCommandResponseHandler::InvalidRequestResultCode,
                "Invalid request",
                responseFrame,
                responseFrameCapacity,
                responseFrameLength));
    }

    const HaseCommandRegistration* registration =
        HaseEndpointDefinitionResolver::ResolveCommand(
            definition,
            request.instrumentId,
            request.commandPath);

    if (registration == nullptr
        || registration->executeNullBoolean == nullptr)
    {
        return FromResponseCreation(
            CreateCommandFailure(
                envelope,
                HaseExecuteCommandResponseHandler::NotFoundResultCode,
                "Command not found",
                responseFrame,
                responseFrameCapacity,
                responseFrameLength));
    }

    bool returnValue =
        false;

    const HaseApplicationResult applicationResult =
        registration->executeNullBoolean(
            registration->context,
            returnValue);

    if (applicationResult == HaseApplicationResult::Unavailable)
    {
        return FromResponseCreation(
            CreateCommandFailure(
                envelope,
                HaseExecuteCommandResponseHandler::InternalErrorResultCode,
                "Command hardware unavailable",
                responseFrame,
                responseFrameCapacity,
                responseFrameLength));
    }

    if (applicationResult != HaseApplicationResult::Success)
    {
        return HaseEndpointRequestProcessingResult::ResponseCreationFailed;
    }

    return FromResponseCreation(
        HaseExecuteCommandResponseHandler::CreateBooleanSuccessResponse(
            envelope,
            returnValue,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength));
}

HaseEndpointRequestProcessingResult
    HaseEndpointRequestProcessor::ProcessReadDescriptor(
        const HaseProtocolEnvelope& envelope,
        const HaseEndpointDefinition& definition,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
{
    if (definition.descriptor == nullptr)
    {
        return HaseEndpointRequestProcessingResult::ResponseCreationFailed;
    }

    return FromResponseCreation(
        HaseReadEndpointDescriptorHandler::CreateResponse(
            envelope,
            *definition.descriptor,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength));
}

HaseEndpointRequestProcessingResult
    HaseEndpointRequestProcessor::FromResponseCreation(
        bool responseCreated)
{
    if (responseCreated)
    {
        return HaseEndpointRequestProcessingResult::ResponseCreated;
    }

    return HaseEndpointRequestProcessingResult::ResponseCreationFailed;
}
