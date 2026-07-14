#include "HaseBinaryProtocolWriter.h"

#include <cstring>

HaseBinaryProtocolWriter::HaseBinaryProtocolWriter(
    uint8_t* buffer,
    size_t capacity)
    : _buffer(buffer),
      _capacity(capacity),
      _position(0),
      _succeeded(
          buffer != nullptr
          || capacity == 0)
{
}

bool HaseBinaryProtocolWriter::writeByte(
    uint8_t value)
{
    if (!_succeeded
        || remaining() < 1)
    {
        _succeeded =
            false;

        return false;
    }

    _buffer[_position] =
        value;

    _position++;

    return true;
}

bool HaseBinaryProtocolWriter::writeUInt16(
    uint16_t value)
{
    if (!_succeeded
        || remaining() < 2)
    {
        _succeeded =
            false;

        return false;
    }

    _buffer[_position] =
        static_cast<uint8_t>(
            value & 0xFF);

    _buffer[_position + 1] =
        static_cast<uint8_t>(
            (value >> 8) & 0xFF);

    _position +=
        2;

    return true;
}

bool HaseBinaryProtocolWriter::writeUInt32(
    uint32_t value)
{
    if (!_succeeded
        || remaining() < 4)
    {
        _succeeded =
            false;

        return false;
    }

    _buffer[_position] =
        static_cast<uint8_t>(
            value & 0xFF);

    _buffer[_position + 1] =
        static_cast<uint8_t>(
            (value >> 8) & 0xFF);

    _buffer[_position + 2] =
        static_cast<uint8_t>(
            (value >> 16) & 0xFF);

    _buffer[_position + 3] =
        static_cast<uint8_t>(
            (value >> 24) & 0xFF);

    _position +=
        4;

    return true;
}

bool HaseBinaryProtocolWriter::writeCount(
    uint16_t count)
{
    return writeUInt16(
        count);
}

bool HaseBinaryProtocolWriter::writeString(
    const char* value)
{
    if (value == nullptr)
    {
        _succeeded =
            false;

        return false;
    }

    size_t byteLength =
        strlen(
            value);

    if (byteLength > UINT16_MAX)
    {
        _succeeded =
            false;

        return false;
    }

    if (!writeUInt16(
            static_cast<uint16_t>(
                byteLength)))
    {
        return false;
    }

    return writeBytes(
        reinterpret_cast<const uint8_t*>(
            value),
        byteLength);
}

bool HaseBinaryProtocolWriter::writeBytes(
    const uint8_t* value,
    size_t length)
{
    if (!_succeeded)
    {
        return false;
    }

    if (length == 0)
    {
        return true;
    }

    if (value == nullptr
        || remaining() < length)
    {
        _succeeded =
            false;

        return false;
    }

    memcpy(
        _buffer + _position,
        value,
        length);

    _position +=
        length;

    return true;
}

size_t HaseBinaryProtocolWriter::length() const
{
    return _position;
}

size_t HaseBinaryProtocolWriter::remaining() const
{
    if (_position >= _capacity)
    {
        return 0;
    }

    return
        _capacity - _position;
}

bool HaseBinaryProtocolWriter::succeeded() const
{
    return _succeeded;
}