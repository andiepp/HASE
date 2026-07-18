#include "HasePhysicalEventPublisher.h"

#include "HaseBinaryProtocolWriter.h"
#include "HaseProtocolEnvelope.h"

HasePhysicalEventPublisher::HasePhysicalEventPublisher(
    HaseTcpTransport& transport,
    const HaseUtcClock& utcClock)
    : _transport(
          transport),
      _utcClock(
          utcClock)
{
}

void HasePhysicalEventPublisher::begin()
{
    pinMode(
        ButtonPin,
        INPUT_PULLUP);

    uint8_t initialLevel =
        digitalRead(
            ButtonPin);

    _rawLevel =
        initialLevel;

    _stableLevel =
        initialLevel;

    _rawLevelChangedAt =
        millis();

    _pressArmed =
        initialLevel == HIGH;

    _initialized =
        true;
}

void HasePhysicalEventPublisher::update()
{
    if (!_initialized)
    {
        return;
    }

    unsigned long now =
        millis();

    uint8_t currentRawLevel =
        digitalRead(
            ButtonPin);

    if (currentRawLevel != _rawLevel)
    {
        _rawLevel =
            currentRawLevel;

        _rawLevelChangedAt =
            now;
    }

    if (_rawLevel == _stableLevel)
    {
        return;
    }

    if (now - _rawLevelChangedAt
        < DebounceMilliseconds)
    {
        return;
    }

    _stableLevel =
        _rawLevel;

    if (_stableLevel == HIGH)
    {
        _pressArmed =
            true;

        return;
    }

    if (!_pressArmed)
    {
        return;
    }

    _pressArmed =
        false;

    if (!_transport.hasConnectedClient())
    {
        return;
    }

    if (!publishButtonPressed()
        && _transport.hasConnectedClient())
    {
        _transport.disconnectClient();
    }
}

bool HasePhysicalEventPublisher::publishButtonPressed()
{
    if (!_transport.hasConnectedClient())
    {
        return false;
    }

    int64_t unixTimeMilliseconds =
        0;

    if (!_utcClock.tryGetUnixTimeMilliseconds(
            unixTimeMilliseconds))
    {
        return false;
    }

    uint8_t frame[
        FrameCapacity];

    uint32_t frameLength =
        0;

    if (!createButtonPressedFrame(
            unixTimeMilliseconds,
            frame,
            sizeof(frame),
            frameLength))
    {
        return false;
    }

    return _transport.writeFrame(
        frame,
        frameLength);
}

bool HasePhysicalEventPublisher::createButtonPressedFrame(
    int64_t unixTimeMilliseconds,
    uint8_t* frame,
    size_t frameCapacity,
    uint32_t& frameLength) const
{
    frameLength =
        0;

    if (frame == nullptr)
    {
        return false;
    }

    uint8_t payload[
        PayloadCapacity];

    HaseBinaryProtocolWriter writer(
        payload,
        sizeof(payload));

    if (!writer.writeString(
            ControllerInstrumentId))
    {
        return false;
    }

    if (!writer.writeString(
            ButtonPressedEventPath))
    {
        return false;
    }

    if (!writer.writeInt64(
            unixTimeMilliseconds))
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
        static_cast<uint32_t>(
            writer.length());

    return HaseProtocolEnvelopeCodec::Encode(
        notification,
        frame,
        frameCapacity,
        frameLength);
}