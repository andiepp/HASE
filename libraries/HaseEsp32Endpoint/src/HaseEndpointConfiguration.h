#pragma once

#include <Arduino.h>

struct HaseEndpointConfiguration
{
    uint16_t tcpPort;

    const char* mdnsHostName;
    const char* mdnsInstanceName;

    uint32_t maximumPayloadLength;
    unsigned long readTimeoutMilliseconds;
    unsigned long utcSynchronizationTimeoutMilliseconds;
};
