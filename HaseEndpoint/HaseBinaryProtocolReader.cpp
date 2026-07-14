#include "HaseBinaryProtocolReader.h"

#include <cstring>

HaseBinaryProtocolReader::HaseBinaryProtocolReader(
    const uint8_t* buffer,
    size_t length)
    : _buffer(buffer),
      _length(length),
      _position(0),
      _succeeded(
          buffer != nullptr
          || length == 0)
{
}

bool HaseBinaryProtocolReader::readByte(
    uint8_t& value)
{
    if (!_succeeded
        || remaining() < 1)
    {
        _succeeded =
            false;

        return false;
    }

    value =
        _buffer[_position];

    _position++;

    return true;
}

bool HaseBinaryProtocolReader::readUInt16(
    uint16_t& value)
{
    if (!_succeeded
        || remaining() < 2)
    {
        _succeeded =
            false;

        return false;
    }

    value =
        static_cast<uint16_t>(
            _buffer[_position])
        | static_cast<uint16_t>(
            static_cast<uint16_t>(
                _buffer[_position + 1])
            << 8);

    _position +=
        2;

    return true;
}

bool HaseBinaryProtocolReader::readUInt32(
    uint32_t& value)
{
    if (!_succeeded
        || remaining() < 4)
    {
        _succeeded =
            false;

        return false;
    }

    value =
        static_cast<uint32_t>(
            _buffer[_position])
        | static_cast<uint32_t>(
            _buffer[_position + 1])
            << 8
        | static_cast<uint32_t>(
            _buffer[_position + 2])
            << 16
        | static_cast<uint32_t>(
            _buffer[_position + 3])
            << 24;

    _position +=
        4;

    return true;
}

bool HaseBinaryProtocolReader::readDouble(
    double& value)
{
    static_assert(
        sizeof(double) == 8,
        "HASE Protocol Version 1 requires 64-bit double values.");

    if (!_succeeded
        || remaining() < sizeof(double))
    {
        _succeeded =
            false;

        return false;
    }

    uint64_t encodedValue =
        0;

    for (size_t index = 0;
         index < sizeof(encodedValue);
         index++)
    {
        encodedValue |=
            static_cast<uint64_t>(
                _buffer[_position + index])
            << (index * 8);
    }

    memcpy(
        &value,
        &encodedValue,
        sizeof(value));

    _position +=
        sizeof(double);

    return true;
}

bool HaseBinaryProtocolReader::readCount(
    uint16_t& count)
{
    return readUInt16(
        count);
}

bool HaseBinaryProtocolReader::readString(
    char* destination,
    size_t destinationCapacity)
{
    if (!_succeeded
        || destination == nullptr
        || destinationCapacity == 0)
    {
        _succeeded =
            false;

        return false;
    }

    uint16_t byteLength =
        0;

    if (!readUInt16(
            byteLength))
    {
        return false;
    }

    if (remaining() < byteLength
        || static_cast<size_t>(byteLength) + 1
            > destinationCapacity)
    {
        _succeeded =
            false;

        return false;
    }

    if (byteLength > 0)
    {
        memcpy(
            destination,
            _buffer + _position,
            byteLength);
    }

    destination[byteLength] =
        '\0';

    _position +=
        byteLength;

    return true;
}

bool HaseBinaryProtocolReader::skipBytes(
    size_t length)
{
    if (!_succeeded
        || remaining() < length)
    {
        _succeeded =
            false;

        return false;
    }

    _position +=
        length;

    return true;
}

size_t HaseBinaryProtocolReader::position() const
{
    return _position;
}

size_t HaseBinaryProtocolReader::remaining() const
{
    if (_position >= _length)
    {
        return 0;
    }

    return
        _length - _position;
}

bool HaseBinaryProtocolReader::fullyConsumed() const
{
    return
        _succeeded
        && _position == _length;
}

bool HaseBinaryProtocolReader::succeeded() const
{
    return _succeeded;
}