#include "HaseEndpointRuntime.h"

#include <WiFi.h>

#include "HaseEndpointDefinitionValidator.h"
#include "HaseEndpointRequestProcessor.h"
#include "HaseEventNotificationHandler.h"

HaseEndpointRuntime::HaseEndpointRuntime(
    const HaseEndpointConfiguration& configuration,
    const HaseEndpointDefinition& definition,
    HaseEndpointApplication& application)
    : _configuration(
          configuration),
      _definition(
          definition),
      _application(
          application),
      _mdnsAdvertiser(),
      _utcClock(),
      _transport(
          configuration.tcpPort,
          configuration.maximumPayloadLength,
          configuration.readTimeoutMilliseconds),
      _requestBuffer{},
      _responseBuffer{}
{
}

bool HaseEndpointRuntime::begin(
    const char* wifiSsid,
    const char* wifiPassword)
{
    _started =
        false;

    if (!validateConfiguration())
    {
        Serial.println(
            "Endpoint configuration validation failed.");

        return false;
    }

    const HaseEndpointDefinitionValidationResult definitionResult =
        HaseEndpointDefinitionValidator::Validate(
            _definition);

    if (definitionResult
        != HaseEndpointDefinitionValidationResult::Valid)
    {
        Serial.print(
            "Endpoint definition validation failed: ");

        Serial.println(
            static_cast<uint8_t>(definitionResult));

        Serial.println(
            "Endpoint startup stopped before publication.");

        return false;
    }

    Serial.print(
        "Endpoint ID   : ");

    Serial.println(
        _definition.descriptor->id);

    if (!_application.beginHardware())
    {
        Serial.println(
            "Endpoint hardware initialization failed.");

        return false;
    }

    if (!connectToWifi(
            wifiSsid,
            wifiPassword))
    {
        Serial.println(
            "Endpoint startup stopped because Wi-Fi configuration is invalid.");

        return false;
    }

    if (!synchronizeUtcClock())
    {
        Serial.println(
            "Endpoint startup stopped because UTC could not be synchronized.");

        return false;
    }

    _application.beginEventDetection();

    startNetworkEndpoint();

    _started =
        true;

    Serial.println(
        "Waiting for HASE client...");

    return true;
}

void HaseEndpointRuntime::update()
{
    if (!_started)
    {
        delay(
            1000);

        return;
    }

    if (WiFi.status() != WL_CONNECTED)
    {
        stopNetworkEndpoint();

        if (!connectToWifi(
                nullptr,
                nullptr))
        {
            _started =
                false;

            return;
        }

        if (!synchronizeUtcClock())
        {
            Serial.println(
                "UTC synchronization failed after Wi-Fi reconnect.");

            Serial.println(
                "Endpoint transport remains stopped.");

            _started =
                false;

            return;
        }

        startNetworkEndpoint();

        Serial.println(
            "Waiting for HASE client...");
    }

    _transport.update();

    _application.update(
        *this);

    processTransport();

    delay(
        1);
}

bool HaseEndpointRuntime::publishNullEvent(
    const HaseEventRegistration& eventRegistration)
{
    if (!_transport.hasConnectedClient())
    {
        return false;
    }

    const bool published =
        HaseEventNotificationHandler::PublishNull(
            eventRegistration,
            _utcClock,
            _transport);

    if (!published
        && _transport.hasConnectedClient())
    {
        _transport.disconnectClient();
    }

    return published;
}

bool HaseEndpointRuntime::isStarted() const
{
    return _started;
}

bool HaseEndpointRuntime::validateConfiguration() const
{
    return
        _configuration.tcpPort != 0
        && _configuration.mdnsHostName != nullptr
        && _configuration.mdnsHostName[0] != '\0'
        && _configuration.mdnsInstanceName != nullptr
        && _configuration.mdnsInstanceName[0] != '\0'
        && _configuration.maximumPayloadLength > 0
        && _configuration.maximumPayloadLength <= BufferCapacity
        && _configuration.readTimeoutMilliseconds > 0
        && _configuration.utcSynchronizationTimeoutMilliseconds > 0;
}

bool HaseEndpointRuntime::connectToWifi(
    const char* wifiSsid,
    const char* wifiPassword)
{
    if (wifiSsid != nullptr)
    {
        _wifiSsid =
            wifiSsid;
    }

    if (wifiPassword != nullptr)
    {
        _wifiPassword =
            wifiPassword;
    }

    if (_wifiSsid == nullptr
        || _wifiSsid[0] == '\0'
        || _wifiPassword == nullptr)
    {
        return false;
    }

    Serial.println(
        "Connecting to Wi-Fi...");

    WiFi.mode(
        WIFI_STA);

    WiFi.begin(
        _wifiSsid,
        _wifiPassword);

    while (WiFi.status() != WL_CONNECTED)
    {
        delay(
            500);

        Serial.print(
            ".");
    }

    Serial.println();

    Serial.println(
        "Wi-Fi connected.");

    return true;
}

bool HaseEndpointRuntime::synchronizeUtcClock()
{
    Serial.println(
        "Synchronizing UTC clock...");

    if (!_utcClock.synchronize(
            _configuration.utcSynchronizationTimeoutMilliseconds))
    {
        Serial.println(
            "UTC synchronization failed.");

        return false;
    }

    int64_t unixTimeMilliseconds =
        0;

    if (!_utcClock.tryGetUnixTimeMilliseconds(
            unixTimeMilliseconds))
    {
        Serial.println(
            "UTC clock synchronized but no valid timestamp could be read.");

        return false;
    }

    Serial.println(
        "UTC clock synchronized.");

    return true;
}

void HaseEndpointRuntime::startNetworkEndpoint()
{
    _transport.begin();

    const bool advertisementStarted =
        _mdnsAdvertiser.begin(
            _configuration.mdnsHostName,
            _configuration.mdnsInstanceName,
            _configuration.tcpPort);

    if (!advertisementStarted)
    {
        Serial.println(
            "Failed to advertise the HASE TCP endpoint through mDNS.");

        return;
    }

    Serial.println(
        "HASE network endpoint advertised through mDNS/DNS-SD.");
}

void HaseEndpointRuntime::stopNetworkEndpoint()
{
    _mdnsAdvertiser.end();

    _transport.disconnectClient();

    Serial.println(
        "HASE network endpoint advertisement stopped.");
}

void HaseEndpointRuntime::processTransport()
{
    if (!_transport.hasAvailableFrame())
    {
        return;
    }

    uint32_t requestLength =
        0;

    if (!_transport.readFrame(
            _requestBuffer,
            sizeof(_requestBuffer),
            requestLength))
    {
        Serial.println(
            "Failed to read TCP frame. Closing client connection.");

        _transport.disconnectClient();

        return;
    }

    printPayload(
        "Received",
        _requestBuffer,
        requestLength);

    HaseProtocolEnvelope envelope;

    const bool envelopeDecoded =
        HaseProtocolEnvelopeCodec::Decode(
            _requestBuffer,
            requestLength,
            envelope);

    if (envelopeDecoded)
    {
        printProtocolEnvelope(
            envelope);

        const HaseProtocolDispatchResult dispatchResult =
            HaseProtocolDispatcher::Dispatch(
                envelope);

        printDispatchResult(
            dispatchResult);

        if (processProtocolFrame(
                envelope,
                dispatchResult))
        {
            return;
        }
    }
    else
    {
        Serial.println(
            "Received payload is not a valid HASE protocol envelope.");
    }

    if (!_transport.writeFrame(
            _requestBuffer,
            requestLength))
    {
        Serial.println(
            "Failed to echo TCP frame. Closing client connection.");

        _transport.disconnectClient();

        return;
    }

    printPayload(
        "Echoed",
        _requestBuffer,
        requestLength);
}

bool HaseEndpointRuntime::processProtocolFrame(
    const HaseProtocolEnvelope& envelope,
    HaseProtocolDispatchResult dispatchResult)
{
    uint32_t responseLength =
        0;

    const HaseEndpointRequestProcessingResult processingResult =
        HaseEndpointRequestProcessor::Process(
            envelope,
            dispatchResult,
            _definition,
            _utcClock,
            _responseBuffer,
            sizeof(_responseBuffer),
            responseLength);

    if (processingResult
        == HaseEndpointRequestProcessingResult::NotHandled)
    {
        return false;
    }

    if (processingResult
        == HaseEndpointRequestProcessingResult::ResponseCreationFailed)
    {
        Serial.println(
            "Failed to create protocol response. Closing client connection.");

        _transport.disconnectClient();

        return true;
    }

    if (!_transport.writeFrame(
            _responseBuffer,
            responseLength))
    {
        Serial.println(
            "Failed to write protocol response. Closing client connection.");

        _transport.disconnectClient();

        return true;
    }

    printPayload(
        "Responded",
        _responseBuffer,
        responseLength);

    return true;
}

void HaseEndpointRuntime::printProtocolEnvelope(
    const HaseProtocolEnvelope& envelope)
{
    Serial.println();
    Serial.println(
        "Protocol Envelope");
    Serial.print(
        "Version        : ");
    Serial.print(
        envelope.majorVersion);
    Serial.print(
        ".");
    Serial.println(
        envelope.minorVersion);
    Serial.print(
        "Role           : ");
    Serial.println(
        envelope.role);
    Serial.print(
        "Message Type   : ");
    Serial.println(
        envelope.messageType);
    Serial.print(
        "Correlation Id : ");
    Serial.println(
        envelope.correlationId);
    Serial.print(
        "Payload Length : ");
    Serial.println(
        envelope.payloadLength);
}

void HaseEndpointRuntime::printDispatchResult(
    HaseProtocolDispatchResult result)
{
    Serial.print(
        "Protocol dispatch result: ");

    Serial.println(
        static_cast<uint8_t>(result));
}

void HaseEndpointRuntime::printPayload(
    const char* caption,
    const uint8_t* payload,
    uint32_t payloadLength)
{
    Serial.print(
        caption);

    Serial.print(
        " ");

    Serial.print(
        payloadLength);

    Serial.print(
        " bytes:");

    for (uint32_t index = 0;
         index < payloadLength;
         index++)
    {
        Serial.print(
            " ");

        if (payload[index] < 0x10)
        {
            Serial.print(
                "0");
        }

        Serial.print(
            payload[index],
            HEX);
    }

    Serial.println();
}
