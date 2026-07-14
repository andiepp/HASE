#include "HaseTcpTransport.h"

HaseTcpTransport::HaseTcpTransport(
    uint16_t port,
    uint32_t maximumPayloadLength,
    unsigned long readTimeoutMilliseconds)
    : _server(port),
      _port(port),
      _maximumPayloadLength(maximumPayloadLength),
      _readTimeoutMilliseconds(readTimeoutMilliseconds)
{
}

void HaseTcpTransport::begin()
{
    _server.begin();

    Serial.print(
        "TCP server listening on port ");

    Serial.println(
        _port);
}

void HaseTcpTransport::update()
{
    acceptClient();

    if (_client
        && !_client.connected())
    {
        Serial.println(
            "Client disconnected.");

        _client.stop();
    }
}

bool HaseTcpTransport::hasConnectedClient()
{
    return
        _client
        && _client.connected();
}

bool HaseTcpTransport::hasAvailableFrame()
{
    return
        hasConnectedClient()
        && _client.available() > 0;
}

bool HaseTcpTransport::readFrame(
    uint8_t* payload,
    size_t payloadCapacity,
    uint32_t& payloadLength)
{
    if (!hasConnectedClient())
    {
        return false;
    }

    if (payload == nullptr
        && payloadCapacity > 0)
    {
        return false;
    }

    uint8_t header[
        FrameHeaderLength];

    if (!readExactly(
            header,
            sizeof(header)))
    {
        return false;
    }

    payloadLength =
        decodeUInt32BigEndian(
            header);

    if (payloadLength > _maximumPayloadLength)
    {
        Serial.print(
            "Rejected frame with payload length ");

        Serial.print(
            payloadLength);

        Serial.print(
            ". Maximum transport length is ");

        Serial.print(
            _maximumPayloadLength);

        Serial.println(
            " bytes.");

        return false;
    }

    if (payloadLength > payloadCapacity)
    {
        Serial.print(
            "Rejected frame with payload length ");

        Serial.print(
            payloadLength);

        Serial.print(
            ". Supplied payload buffer holds ");

        Serial.print(
            payloadCapacity);

        Serial.println(
            " bytes.");

        return false;
    }

    if (payloadLength == 0)
    {
        return true;
    }

    return readExactly(
        payload,
        payloadLength);
}

bool HaseTcpTransport::writeFrame(
    const uint8_t* payload,
    uint32_t payloadLength)
{
    if (!hasConnectedClient())
    {
        return false;
    }

    if (payload == nullptr
        && payloadLength > 0)
    {
        return false;
    }

    if (payloadLength > _maximumPayloadLength)
    {
        return false;
    }

    uint8_t header[
        FrameHeaderLength];

    encodeUInt32BigEndian(
        payloadLength,
        header);

    if (!writeExactly(
            header,
            sizeof(header)))
    {
        return false;
    }

    if (payloadLength == 0)
    {
        return true;
    }

    return writeExactly(
        payload,
        payloadLength);
}

void HaseTcpTransport::disconnectClient()
{
    if (_client)
    {
        _client.stop();
    }
}

void HaseTcpTransport::acceptClient()
{
    if (hasConnectedClient())
    {
        return;
    }

    if (_client)
    {
        _client.stop();
    }

    WiFiClient newClient =
        _server.available();

    if (!newClient)
    {
        return;
    }

    _client =
        newClient;

    _client.setNoDelay(
        true);

    Serial.println();

    Serial.print(
        "Client connected from ");

    Serial.print(
        _client.remoteIP());

    Serial.print(
        ":");

    Serial.println(
        _client.remotePort());
}

bool HaseTcpTransport::readExactly(
    uint8_t* buffer,
    size_t length)
{
    size_t offset =
        0;

    unsigned long lastProgressAt =
        millis();

    while (offset < length)
    {
        if (!hasConnectedClient())
        {
            return false;
        }

        int availableBytes =
            _client.available();

        if (availableBytes > 0)
        {
            size_t remainingLength =
                length - offset;

            size_t requestedLength =
                static_cast<size_t>(
                    availableBytes);

            if (requestedLength > remainingLength)
            {
                requestedLength =
                    remainingLength;
            }

            int bytesRead =
                _client.read(
                    buffer + offset,
                    requestedLength);

            if (bytesRead > 0)
            {
                offset +=
                    static_cast<size_t>(
                        bytesRead);

                lastProgressAt =
                    millis();

                continue;
            }
        }

        unsigned long elapsed =
            millis() - lastProgressAt;

        if (elapsed >= _readTimeoutMilliseconds)
        {
            Serial.println(
                "TCP read timeout.");

            return false;
        }

        delay(
            1);
    }

    return true;
}

bool HaseTcpTransport::writeExactly(
    const uint8_t* buffer,
    size_t length)
{
    size_t offset =
        0;

    while (offset < length)
    {
        if (!hasConnectedClient())
        {
            return false;
        }

        size_t bytesWritten =
            _client.write(
                buffer + offset,
                length - offset);

        if (bytesWritten == 0)
        {
            return false;
        }

        offset +=
            bytesWritten;
    }

    return true;
}

uint32_t HaseTcpTransport::decodeUInt32BigEndian(
    const uint8_t* buffer)
{
    return
        (static_cast<uint32_t>(buffer[0]) << 24)
        | (static_cast<uint32_t>(buffer[1]) << 16)
        | (static_cast<uint32_t>(buffer[2]) << 8)
        | static_cast<uint32_t>(buffer[3]);
}

void HaseTcpTransport::encodeUInt32BigEndian(
    uint32_t value,
    uint8_t* buffer)
{
    buffer[0] =
        static_cast<uint8_t>(
            (value >> 24) & 0xFF);

    buffer[1] =
        static_cast<uint8_t>(
            (value >> 16) & 0xFF);

    buffer[2] =
        static_cast<uint8_t>(
            (value >> 8) & 0xFF);

    buffer[3] =
        static_cast<uint8_t>(
            value & 0xFF);
}