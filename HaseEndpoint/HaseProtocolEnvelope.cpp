#include "HaseProtocolEnvelope.h"

#include <cstring>

bool HaseProtocolEnvelopeCodec::Decode(
    const uint8_t* frame,
    uint32_t frameLength,
    HaseProtocolEnvelope& envelope)
{
    if (frame == nullptr)
    {
        return false;
    }

    if (frameLength < HeaderLength)
    {
        return false;
    }

    envelope.majorVersion =
        frame[0];

    envelope.minorVersion =
        frame[1];

    envelope.role =
        frame[2];

    envelope.messageType =
        frame[3];

    envelope.correlationId =
        ReadUInt32LittleEndian(
            frame + 4);

    envelope.payloadLength =
        ReadUInt32LittleEndian(
            frame + 8);

    uint64_t expectedFrameLength =
        static_cast<uint64_t>(
            HeaderLength)
        + envelope.payloadLength;

    if (expectedFrameLength
        != frameLength)
    {
        return false;
    }

    envelope.payload =
        frame + HeaderLength;

    return true;
}

bool HaseProtocolEnvelopeCodec::Encode(
    const HaseProtocolEnvelope& envelope,
    uint8_t* frame,
    size_t frameCapacity,
    uint32_t& frameLength)
{
    frameLength =
        0;

    if (frame == nullptr)
    {
        return false;
    }

    uint64_t requiredFrameLength =
        static_cast<uint64_t>(
            HeaderLength)
        + envelope.payloadLength;

    if (requiredFrameLength
        > frameCapacity)
    {
        return false;
    }

    if (envelope.payloadLength > 0
        && envelope.payload == nullptr)
    {
        return false;
    }

    frame[0] =
        envelope.majorVersion;

    frame[1] =
        envelope.minorVersion;

    frame[2] =
        envelope.role;

    frame[3] =
        envelope.messageType;

    WriteUInt32LittleEndian(
        envelope.correlationId,
        frame + 4);

    WriteUInt32LittleEndian(
        envelope.payloadLength,
        frame + 8);

    if (envelope.payloadLength > 0)
    {
        memcpy(
            frame + HeaderLength,
            envelope.payload,
            envelope.payloadLength);
    }

    frameLength =
        static_cast<uint32_t>(
            requiredFrameLength);

    return true;
}

uint32_t HaseProtocolEnvelopeCodec::ReadUInt32LittleEndian(
    const uint8_t* buffer)
{
    return
        static_cast<uint32_t>(
            buffer[0])
        |
        (static_cast<uint32_t>(
            buffer[1]) << 8)
        |
        (static_cast<uint32_t>(
            buffer[2]) << 16)
        |
        (static_cast<uint32_t>(
            buffer[3]) << 24);
}

void HaseProtocolEnvelopeCodec::WriteUInt32LittleEndian(
    uint32_t value,
    uint8_t* buffer)
{
    buffer[0] =
        static_cast<uint8_t>(
            value & 0xFF);

    buffer[1] =
        static_cast<uint8_t>(
            (value >> 8) & 0xFF);

    buffer[2] =
        static_cast<uint8_t>(
            (value >> 16) & 0xFF);

    buffer[3] =
        static_cast<uint8_t>(
            (value >> 24) & 0xFF);
}