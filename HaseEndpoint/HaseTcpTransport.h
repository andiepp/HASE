#pragma once

#include <Arduino.h>
#include <WiFi.h>

class HaseTcpTransport
{
public:
    HaseTcpTransport(
        uint16_t port,
        uint32_t maximumPayloadLength,
        unsigned long readTimeoutMilliseconds);

    void begin();

    void update();

    bool hasConnectedClient();

    bool hasAvailableFrame();

    bool readFrame(
        uint8_t* payload,
        size_t payloadCapacity,
        uint32_t& payloadLength);

    bool writeFrame(
        const uint8_t* payload,
        uint32_t payloadLength);

    void disconnectClient();

private:
    static constexpr size_t FrameHeaderLength =
        4;

    WiFiServer _server;
    WiFiClient _client;

    uint16_t _port;
    uint32_t _maximumPayloadLength;
    unsigned long _readTimeoutMilliseconds;

    void acceptClient();

    bool readExactly(
        uint8_t* buffer,
        size_t length);

    bool writeExactly(
        const uint8_t* buffer,
        size_t length);

    static uint32_t decodeUInt32BigEndian(
        const uint8_t* buffer);

    static void encodeUInt32BigEndian(
        uint32_t value,
        uint8_t* buffer);
};