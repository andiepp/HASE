#include <WiFi.h>

#include "HaseProtocolDispatcher.h"
#include "HaseProtocolEnvelope.h"
#include "HaseTcpTransport.h"

// -----------------------------------------------------------------------------
// Wi-Fi configuration
// -----------------------------------------------------------------------------

const char* WIFI_SSID =
    "Vodafone-C5E4";

const char* WIFI_PASSWORD =
    "Sonnenhut1234";

// -----------------------------------------------------------------------------
// HASE transport configuration
// -----------------------------------------------------------------------------

constexpr uint16_t TCP_PORT =
    5000;

constexpr uint32_t MAXIMUM_PAYLOAD_LENGTH =
    4096;

constexpr unsigned long READ_TIMEOUT_MILLISECONDS =
    5000;

// -----------------------------------------------------------------------------
// Global objects and buffers
// -----------------------------------------------------------------------------

HaseTcpTransport transport(
    TCP_PORT,
    MAXIMUM_PAYLOAD_LENGTH,
    READ_TIMEOUT_MILLISECONDS);

uint8_t payloadBuffer[
    MAXIMUM_PAYLOAD_LENGTH];

// -----------------------------------------------------------------------------
// Forward declarations
// -----------------------------------------------------------------------------

void connectToWiFi();

void processTransport();

void printProtocolEnvelope(
    const HaseProtocolEnvelope& envelope);

void printDispatchResult(
    HaseProtocolDispatchResult result);

void printPayload(
    const char* caption,
    const uint8_t* payload,
    uint32_t payloadLength);

// -----------------------------------------------------------------------------
// Arduino lifecycle
// -----------------------------------------------------------------------------

void setup()
{
    Serial.begin(
        115200);

    delay(
        500);

    Serial.println();

    Serial.println(
        "HASE ESP32 Endpoint");

    Serial.println(
        "===================");

    connectToWiFi();

    transport.begin();

    Serial.println(
        "Waiting for HASE client...");
}

void loop()
{
    if (WiFi.status() != WL_CONNECTED)
    {
        transport.disconnectClient();

        connectToWiFi();

        transport.begin();

        Serial.println(
            "Waiting for HASE client...");
    }

    transport.update();

    processTransport();

    delay(
        1);
}

// -----------------------------------------------------------------------------
// Wi-Fi
// -----------------------------------------------------------------------------

void connectToWiFi()
{
    Serial.print(
        "Connecting to Wi-Fi network: ");

    Serial.println(
        WIFI_SSID);

    WiFi.mode(
        WIFI_STA);

    WiFi.begin(
        WIFI_SSID,
        WIFI_PASSWORD);

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

    Serial.print(
        "ESP32 IP address: ");

    Serial.println(
        WiFi.localIP());

    Serial.print(
        "Signal strength: ");

    Serial.print(
        WiFi.RSSI());

    Serial.println(
        " dBm");
}

// -----------------------------------------------------------------------------
// Transport processing
// -----------------------------------------------------------------------------

void processTransport()
{
    if (!transport.hasAvailableFrame())
    {
        return;
    }

    uint32_t payloadLength =
        0;

    bool frameRead =
        transport.readFrame(
            payloadBuffer,
            sizeof(payloadBuffer),
            payloadLength);

    if (!frameRead)
    {
        Serial.println(
            "Failed to read TCP frame. Closing client connection.");

        transport.disconnectClient();

        return;
    }

    printPayload(
        "Received",
        payloadBuffer,
        payloadLength);

    HaseProtocolEnvelope envelope;

    bool envelopeDecoded =
        HaseProtocolEnvelopeCodec::Decode(
            payloadBuffer,
            payloadLength,
            envelope);

    if (envelopeDecoded)
    {
        printProtocolEnvelope(
            envelope);

        HaseProtocolDispatchResult dispatchResult =
            HaseProtocolDispatcher::Dispatch(
                envelope);

        printDispatchResult(
            dispatchResult);
    }
    else
    {
        Serial.println();

        Serial.println(
            "Received payload is not a valid HASE protocol envelope.");

        Serial.println();
    }

    bool frameWritten =
        transport.writeFrame(
            payloadBuffer,
            payloadLength);

    if (!frameWritten)
    {
        Serial.println(
            "Failed to write TCP frame. Closing client connection.");

        transport.disconnectClient();

        return;
    }

    printPayload(
        "Echoed",
        payloadBuffer,
        payloadLength);
}

// -----------------------------------------------------------------------------
// Protocol diagnostics
// -----------------------------------------------------------------------------

void printProtocolEnvelope(
    const HaseProtocolEnvelope& envelope)
{
    Serial.println();

    Serial.println(
        "Protocol Envelope");

    Serial.println(
        "-----------------");

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

    Serial.println();
}

void printDispatchResult(
    HaseProtocolDispatchResult result)
{
    Serial.println(
        "Protocol Dispatch");

    Serial.println(
        "-----------------");

    Serial.print(
        "Result : ");

    switch (result)
    {
        case HaseProtocolDispatchResult::
            DiscoverRequestRecognized:
        {
            Serial.println(
                "DiscoverRequest recognized");

            break;
        }

        case HaseProtocolDispatchResult::
            UnsupportedVersion:
        {
            Serial.println(
                "Unsupported protocol version");

            break;
        }

        case HaseProtocolDispatchResult::
            InvalidDiscoverRequest:
        {
            Serial.println(
                "Invalid DiscoverRequest");

            break;
        }

        case HaseProtocolDispatchResult::
            UnsupportedMessage:
        {
            Serial.println(
                "Unsupported protocol message");

            break;
        }
    }

    Serial.println();
}

// -----------------------------------------------------------------------------
// Byte diagnostics
// -----------------------------------------------------------------------------

void printPayload(
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
        " byte");

    if (payloadLength != 1)
    {
        Serial.print(
            "s");
    }

    Serial.print(
        ":");

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