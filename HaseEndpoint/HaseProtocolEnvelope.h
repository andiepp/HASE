#pragma once

#include <Arduino.h>

struct HaseProtocolEnvelope
{
    uint8_t majorVersion;
    uint8_t minorVersion;

    uint8_t role;

    uint8_t messageType;

    uint32_t correlationId;

    const uint8_t* payload;

    uint32_t payloadLength;
};

class HaseProtocolEnvelopeCodec
{
public:
    static constexpr uint32_t HeaderLength =
        12;

    static bool Decode(
        const uint8_t* frame,
        uint32_t frameLength,
        HaseProtocolEnvelope& envelope);

    static bool Encode(
        const HaseProtocolEnvelope& envelope,
        uint8_t* frame,
        size_t frameCapacity,
        uint32_t& frameLength);

private:
    static uint32_t ReadUInt32LittleEndian(
        const uint8_t* buffer);

    static void WriteUInt32LittleEndian(
        uint32_t value,
        uint8_t* buffer);
};