#pragma once

#include <Arduino.h>

class HaseBinaryProtocolReader
{
public:
    HaseBinaryProtocolReader(
        const uint8_t* buffer,
        size_t length);

    bool readByte(
        uint8_t& value);

    bool readUInt16(
        uint16_t& value);

    bool readUInt32(
        uint32_t& value);

    bool readDouble(
        double& value);

    bool readCount(
        uint16_t& count);

    bool readString(
        char* destination,
        size_t destinationCapacity);

    bool skipBytes(
        size_t length);

    size_t position() const;

    size_t remaining() const;

    bool fullyConsumed() const;

    bool succeeded() const;

private:
    const uint8_t* _buffer;
    size_t _length;
    size_t _position;
    bool _succeeded;
};