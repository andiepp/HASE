#include "HasePhysicalWritePropertyHandler.h"

#include "HaseWritePropertyRequest.h"
#include "HaseWritePropertyResponseHandler.h"

namespace
{
    bool CreateInvalidRequestResponse(
        const HaseProtocolEnvelope& envelope,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
    {
        return
            HaseWritePropertyResponseHandler::
                CreateFailureResponse(
                    envelope,
                    HaseWritePropertyResponseHandler::
                        InvalidRequestResultCode,
                    "Invalid request",
                    responseFrame,
                    responseFrameCapacity,
                    responseFrameLength);
    }

    bool CreateNotFoundResponse(
        const HaseProtocolEnvelope& envelope,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
    {
        return
            HaseWritePropertyResponseHandler::
                CreateFailureResponse(
                    envelope,
                    HaseWritePropertyResponseHandler::
                        NotFoundResultCode,
                    "Property not found",
                    responseFrame,
                    responseFrameCapacity,
                    responseFrameLength);
    }

    bool CreateInternalErrorResponse(
        const HaseProtocolEnvelope& envelope,
        const char* message,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
    {
        return
            HaseWritePropertyResponseHandler::
                CreateFailureResponse(
                    envelope,
                    HaseWritePropertyResponseHandler::
                        InternalErrorResultCode,
                    message,
                    responseFrame,
                    responseFrameCapacity,
                    responseFrameLength);
    }
}

bool HasePhysicalWritePropertyHandler::CreateResponse(
    const HaseProtocolEnvelope& envelope,
    HasePhysicalPropertyService& propertyService,
    HaseUtcClock& utcClock,
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

    HaseWritePropertyRequest request;

    if (!HaseWritePropertyRequestDecoder::DecodeBoolean(
            envelope,
            request))
    {
        return CreateInvalidRequestResponse(
            envelope,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength);
    }

    HasePhysicalPropertyWriteResult writeResult =
        propertyService.writeBoolean(
            request.instrumentId,
            request.propertyId,
            request.value);

    switch (writeResult)
    {
        case HasePhysicalPropertyWriteResult::Success:
        {
            int64_t timestamp;

            if (!utcClock.tryGetUnixTimeMilliseconds(
                    timestamp))
            {
                return CreateInternalErrorResponse(
                    envelope,
                    "UTC clock unavailable",
                    responseFrame,
                    responseFrameCapacity,
                    responseFrameLength);
            }

            return
                HaseWritePropertyResponseHandler::
                    CreateBooleanSuccessResponse(
                        envelope,
                        request.value,
                        timestamp,
                        responseFrame,
                        responseFrameCapacity,
                        responseFrameLength);
        }

        case HasePhysicalPropertyWriteResult::
            InstrumentNotFound:

        case HasePhysicalPropertyWriteResult::
            PropertyNotFound:
        {
            return CreateNotFoundResponse(
                envelope,
                responseFrame,
                responseFrameCapacity,
                responseFrameLength);
        }

        case HasePhysicalPropertyWriteResult::
            HardwareUnavailable:
        {
            return CreateInternalErrorResponse(
                envelope,
                "Property hardware unavailable",
                responseFrame,
                responseFrameCapacity,
                responseFrameLength);
        }
    }

    return false;
}