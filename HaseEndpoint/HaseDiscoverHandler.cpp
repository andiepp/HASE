#include "HaseDiscoverHandler.h"

#include "HaseBinaryProtocolWriter.h"

namespace
{
    constexpr size_t DiscoveryPayloadCapacity =
        192;

    const char* EndpointIdentifier =
        "doit-esp32-devkitc-v4-01";

    const char* EnvironmentSensorInstrumentIdentifier =
        "environment-sensor-01";

    const char* ControllerInstrumentIdentifier =
        "controller-01";
}

bool HaseDiscoverHandler::CreateResponse(
    const HaseProtocolEnvelope& request,
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

    if (!writer.writeString(
            EndpointIdentifier))
    {
        return false;
    }

    if (!writer.writeCount(
            2))
    {
        return false;
    }

    if (!writer.writeString(
            EnvironmentSensorInstrumentIdentifier))
    {
        return false;
    }

    if (!writer.writeString(
            ControllerInstrumentIdentifier))
    {
        return false;
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
        static_cast<uint32_t>(
            writer.length());

    return HaseProtocolEnvelopeCodec::Encode(
        response,
        responseFrame,
        responseFrameCapacity,
        responseFrameLength);
}

const char* HaseDiscoverHandler::EndpointId()
{
    return EndpointIdentifier;
}

const char* HaseDiscoverHandler::InstrumentId()
{
    return EnvironmentSensorInstrumentIdentifier;
}