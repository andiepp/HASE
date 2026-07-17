#include "HasePhysicalReadPropertyHandler.h"

#include "HaseReadPropertyResponseHandler.h"

namespace
{
    bool CreateDoubleResponse(
        const HaseProtocolEnvelope& envelope,
        double value,
        HaseUtcClock& utcClock,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
    {
        int64_t timestamp;

        if (!utcClock.tryGetUnixTimeMilliseconds(
                timestamp))
        {
            return
                HaseReadPropertyResponseHandler::
                    CreateFailureResponse(
                        envelope,
                        HaseReadPropertyResponseHandler::
                            InternalErrorResultCode,
                        "UTC clock unavailable",
                        responseFrame,
                        responseFrameCapacity,
                        responseFrameLength);
        }

        return
            HaseReadPropertyResponseHandler::
                CreateDoubleSuccessResponse(
                    envelope,
                    value,
                    timestamp,
                    responseFrame,
                    responseFrameCapacity,
                    responseFrameLength);
    }

    bool CreateBooleanResponse(
        const HaseProtocolEnvelope& envelope,
        bool value,
        HaseUtcClock& utcClock,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
    {
        int64_t timestamp;

        if (!utcClock.tryGetUnixTimeMilliseconds(
                timestamp))
        {
            return
                HaseReadPropertyResponseHandler::
                    CreateFailureResponse(
                        envelope,
                        HaseReadPropertyResponseHandler::
                            InternalErrorResultCode,
                        "UTC clock unavailable",
                        responseFrame,
                        responseFrameCapacity,
                        responseFrameLength);
        }

        return
            HaseReadPropertyResponseHandler::
                CreateBooleanSuccessResponse(
                    envelope,
                    value,
                    timestamp,
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
            HaseReadPropertyResponseHandler::
                CreateFailureResponse(
                    envelope,
                    HaseReadPropertyResponseHandler::
                        NotFoundResultCode,
                    "Property not found",
                    responseFrame,
                    responseFrameCapacity,
                    responseFrameLength);
    }

    bool CreateUnavailableResponse(
        const HaseProtocolEnvelope& envelope,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
    {
        return
            HaseReadPropertyResponseHandler::
                CreateFailureResponse(
                    envelope,
                    HaseReadPropertyResponseHandler::
                        InternalErrorResultCode,
                    "Property unavailable",
                    responseFrame,
                    responseFrameCapacity,
                    responseFrameLength);
    }
}

bool HasePhysicalReadPropertyHandler::CreateResponse(
    const HaseProtocolEnvelope& envelope,
    const HaseReadPropertyRequest& request,
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

    double doubleValue =
        0.0;

    HasePhysicalPropertyReadResult doubleResult =
        propertyService.readDouble(
            request.instrumentId,
            request.propertyId,
            doubleValue);

    if (doubleResult
        == HasePhysicalPropertyReadResult::Success)
    {
        return CreateDoubleResponse(
            envelope,
            doubleValue,
            utcClock,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength);
    }

    if (doubleResult
        == HasePhysicalPropertyReadResult::PropertyNotFound)
    {
        return CreateNotFoundResponse(
            envelope,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength);
    }

    if (doubleResult
        == HasePhysicalPropertyReadResult::SensorUnavailable)
    {
        return CreateUnavailableResponse(
            envelope,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength);
    }

    bool booleanValue =
        false;

    HasePhysicalPropertyReadResult booleanResult =
        propertyService.readBoolean(
            request.instrumentId,
            request.propertyId,
            booleanValue);

    switch (booleanResult)
    {
        case HasePhysicalPropertyReadResult::Success:
        {
            return CreateBooleanResponse(
                envelope,
                booleanValue,
                utcClock,
                responseFrame,
                responseFrameCapacity,
                responseFrameLength);
        }

        case HasePhysicalPropertyReadResult::InstrumentNotFound:

        case HasePhysicalPropertyReadResult::PropertyNotFound:
        {
            return CreateNotFoundResponse(
                envelope,
                responseFrame,
                responseFrameCapacity,
                responseFrameLength);
        }

        case HasePhysicalPropertyReadResult::SensorUnavailable:
        {
            return CreateUnavailableResponse(
                envelope,
                responseFrame,
                responseFrameCapacity,
                responseFrameLength);
        }
    }

    return false;
}