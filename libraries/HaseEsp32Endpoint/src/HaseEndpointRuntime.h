#pragma once

#include <Arduino.h>

#include "HaseEndpointApplication.h"
#include "HaseEndpointConfiguration.h"
#include "HaseEndpointDefinition.h"
#include "HaseMdnsAdvertiser.h"
#include "HaseProtocolDispatcher.h"
#include "HaseProtocolEnvelope.h"
#include "HaseTcpTransport.h"
#include "HaseUtcClock.h"

class HaseEndpointRuntime
{
public:
    static constexpr uint32_t BufferCapacity =
        4096;

    HaseEndpointRuntime(
        const HaseEndpointConfiguration& configuration,
        const HaseEndpointDefinition& definition,
        HaseEndpointApplication& application);

    bool begin(
        const char* wifiSsid,
        const char* wifiPassword);

    void update();

    bool publishNullEvent(
        const HaseEventRegistration& eventRegistration);

    bool isStarted() const;

private:
    HaseEndpointConfiguration _configuration;
    const HaseEndpointDefinition& _definition;
    HaseEndpointApplication& _application;

    HaseMdnsAdvertiser _mdnsAdvertiser;
    HaseUtcClock _utcClock;
    HaseTcpTransport _transport;

    uint8_t _requestBuffer[BufferCapacity];
    uint8_t _responseBuffer[BufferCapacity];

    const char* _wifiSsid =
        nullptr;

    const char* _wifiPassword =
        nullptr;

    bool _started =
        false;

    bool validateConfiguration() const;

    bool connectToWifi(
        const char* wifiSsid,
        const char* wifiPassword);

    bool synchronizeUtcClock();

    void startNetworkEndpoint();

    void stopNetworkEndpoint();

    void processTransport();

    bool processProtocolFrame(
        const HaseProtocolEnvelope& envelope,
        HaseProtocolDispatchResult dispatchResult);

    static void printProtocolEnvelope(
        const HaseProtocolEnvelope& envelope);

    static void printDispatchResult(
        HaseProtocolDispatchResult result);

    static void printPayload(
        const char* caption,
        const uint8_t* payload,
        uint32_t payloadLength);
};
