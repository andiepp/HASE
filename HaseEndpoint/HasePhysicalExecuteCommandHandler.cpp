#include "HasePhysicalExecuteCommandHandler.h"

#include "HaseExecuteCommandRequest.h"
#include "HaseExecuteCommandResponseHandler.h"

namespace
{
    bool CreateInvalidRequestResponse(
        const HaseProtocolEnvelope& envelope,
        uint8_t* responseFrame,
        size_t responseFrameCapacity,
        uint32_t& responseFrameLength)
    {
        return
            HaseExecuteCommandResponseHandler::
                CreateFailureResponse(
                    envelope,
                    HaseExecuteCommandResponseHandler::
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
            HaseExecuteCommandResponseHandler::
                CreateFailureResponse(
                    envelope,
                    HaseExecuteCommandResponseHandler::
                        NotFoundResultCode,
                    "Command not found",
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
            HaseExecuteCommandResponseHandler::
                CreateFailureResponse(
                    envelope,
                    HaseExecuteCommandResponseHandler::
                        InternalErrorResultCode,
                    message,
                    responseFrame,
                    responseFrameCapacity,
                    responseFrameLength);
    }
}

bool HasePhysicalExecuteCommandHandler::CreateResponse(
    const HaseProtocolEnvelope& envelope,
    HasePhysicalPropertyService& propertyService,
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

    HaseExecuteCommandRequest request;

    if (!HaseExecuteCommandRequestDecoder::
            DecodeNullArgument(
                envelope,
                request))
    {
        return CreateInvalidRequestResponse(
            envelope,
            responseFrame,
            responseFrameCapacity,
            responseFrameLength);
    }

    bool enabled =
        false;

    HasePhysicalCommandExecutionResult executionResult =
        propertyService.toggleStatusLed(
            request.instrumentId,
            request.commandPath,
            enabled);

    switch (executionResult)
    {
        case HasePhysicalCommandExecutionResult::Success:
        {
            return
                HaseExecuteCommandResponseHandler::
                    CreateBooleanSuccessResponse(
                        envelope,
                        enabled,
                        responseFrame,
                        responseFrameCapacity,
                        responseFrameLength);
        }

        case HasePhysicalCommandExecutionResult::
            InstrumentNotFound:

        case HasePhysicalCommandExecutionResult::
            CommandNotFound:
        {
            return CreateNotFoundResponse(
                envelope,
                responseFrame,
                responseFrameCapacity,
                responseFrameLength);
        }

        case HasePhysicalCommandExecutionResult::
            HardwareUnavailable:
        {
            return CreateInternalErrorResponse(
                envelope,
                "Command hardware unavailable",
                responseFrame,
                responseFrameCapacity,
                responseFrameLength);
        }
    }

    return false;
}