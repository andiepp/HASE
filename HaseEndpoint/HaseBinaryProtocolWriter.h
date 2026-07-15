#pragma once

#include <Arduino.h>

class HaseBinaryProtocolWriter
{
public:
    HaseBinaryProtocolWriter(
        uint8_t* buffer,
        size_t capacity);

    bool writeByte(
        uint8_t value);

    bool writeUInt16(
        uint16_t value);

    bool writeUInt32(
        uint32_t value);

    bool writeInt64(
        int64_t value);

    bool writeDouble(
        double value);

    bool writeCount(
        uint16_t count);

    bool writeString(
        const char* value);

    bool writeBytes(
        const uint8_t* value,
        size_t length);

    size_t length() const;

    size_t remaining() const;

    bool succeeded() const;

private:
    uint8_t* _buffer;
    size_t _capacity;
    size_t _position;
    bool _succeeded;
};