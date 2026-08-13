#include "HaseEventNotificationHandler.h"

#include "HaseBinaryProtocolWriter.h"
#include "HaseProtocolEnvelope.h"

bool HaseEventNotificationHandler::PublishNull(
    const HaseEventRegistration& registration,
    const HaseUtcClock& utcClock,
    HaseTcpTransport& transport)
{
    if (!transport.hasConnectedClient())
    {
        return false;
    }

    int64_t unixTimeMilliseconds =
        0;

    if (!utcClock.tryGetUnixTimeMilliseconds(
            unixTimeMilliseconds))
    {
        return false;
    }

    uint8_t frame[
        FrameCapacity];

    uint32_t frameLength =
        0;

    if (!CreateNullFrame(
            registration,
            unixTimeMilliseconds,
            frame,
            sizeof(frame),
            frameLength))
    {
        return false;
    }

    return transport.writeFrame(
        frame,
        frameLength);
}

bool HaseEventNotificationHandler::CreateNullFrame(
    const HaseEventRegistration& registration,
    int64_t unixTimeMilliseconds,
    uint8_t* frame,
    size_t frameCapacity,
    uint32_t& frameLength)
{
    frameLength =
        0;

    if (registration.instrument == nullptr
        || registration.event == nullptr
        || frame == nullptr)
    {
        return false;
    }

    uint8_t payload[
        PayloadCapacity];

    HaseBinaryProtocolWriter writer(
        payload,
        sizeof(payload));

    if (!writer.writeString(registration.instrument->id)
        || !writer.writeString(registration.event->path)
        || !writer.writeInt64(unixTimeMilliseconds)
        || !writer.writeByte(NullVariantType)
        || !writer.succeeded())
    {
        return false;
    }

    HaseProtocolEnvelope notification;

    notification.majorVersion =
        ProtocolMajorVersion;

    notification.minorVersion =
        ProtocolMinorVersion;

    notification.role =
        NotificationRole;

    notification.messageType =
        EventNotificationMessageType;

    notification.correlationId =
        NotificationCorrelationId;

    notification.payload =
        payload;

    notification.payloadLength =
        static_cast<uint32_t>(writer.length());

    return HaseProtocolEnvelopeCodec::Encode(
        notification,
        frame,
        frameCapacity,
        frameLength);
}
