// -----------------------------------------------------------------------------
// HASE Endpoint ESP32
// -----------------------------------------------------------------------------

#include <WiFi.h>

#include "HaseBme280Sensor.h"
#include "HaseDiscoverHandler.h"
#include "HasePhysicalEndpointDescriptor.h"
#include "HaseProtocolDispatcher.h"
#include "HaseProtocolEnvelope.h"
#include "HaseReadEndpointDescriptorHandler.h"
#include "HaseSecrets.h"
#include "HaseTcpTransport.h"

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

HaseBme280Sensor environmentSensor;

HaseTcpTransport transport(
    TCP_PORT,
    MAXIMUM_PAYLOAD_LENGTH,
    READ_TIMEOUT_MILLISECONDS);

uint8_t requestBuffer[
    MAXIMUM_PAYLOAD_LENGTH];

uint8_t responseBuffer[
    MAXIMUM_PAYLOAD_LENGTH];

bool endpointStarted =
    false;

// -----------------------------------------------------------------------------
// Forward declarations
// -----------------------------------------------------------------------------

bool initializeEnvironmentSensor();

void printEnvironmentSensorReading();

void connectToWiFi();

void processTransport();

bool processProtocolFrame(
    const HaseProtocolEnvelope& envelope,
    HaseProtocolDispatchResult dispatchResult);

bool processDiscoverRequest(
    const HaseProtocolEnvelope& envelope);

bool processReadPropertyRequest(
    const HaseProtocolEnvelope& envelope);

bool processReadEndpointDescriptorRequest(
    const HaseProtocolEnvelope& envelope);

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

    Serial.println();

    Serial.print(
        "Endpoint ID   : ");

    Serial.println(
        HaseDiscoverHandler::EndpointId());

    Serial.print(
        "Instrument ID : ");

    Serial.println(
        HaseDiscoverHandler::InstrumentId());

    Serial.println();

    if (!initializeEnvironmentSensor())
    {
        Serial.println(
            "Endpoint startup stopped because the BME280 "
            "could not be initialized.");

        Serial.println();

        return;
    }

    connectToWiFi();

    transport.begin();

    endpointStarted =
        true;

    Serial.println(
        "Waiting for HASE client...");
}

void loop()
{
    if (!endpointStarted)
    {
        delay(
            1000);

        return;
    }

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
// Environment sensor
// -----------------------------------------------------------------------------

bool initializeEnvironmentSensor()
{
    Serial.println(
        "Initializing BME280 environment sensor...");

    Serial.println(
        "I2C configuration:");

    Serial.println(
        "  SDA     : GPIO21");

    Serial.println(
        "  SCL     : GPIO22");

    Serial.println(
        "  Address : 0x76");

    Serial.println();

    if (!environmentSensor.begin())
    {
        Serial.println(
            "BME280 initialization failed.");

        Serial.println(
            "Check the sensor type, I2C address, wiring, and power.");

        Serial.println();

        return false;
    }

    Serial.println(
        "BME280 initialized.");

    Serial.print(
        "Sensor ID : 0x");

    Serial.println(
        environmentSensor.sensorId(),
        HEX);

    Serial.println();

    printEnvironmentSensorReading();

    return true;
}

void printEnvironmentSensorReading()
{
    float temperature =
        environmentSensor.readTemperatureCelsius();

    float relativeHumidity =
        environmentSensor.readRelativeHumidity();

    float airPressure =
        environmentSensor.readAirPressureHectopascal();

    Serial.println(
        "Initial BME280 Reading");

    Serial.println(
        "----------------------");

    Serial.print(
        "Temperature       : ");

    Serial.print(
        temperature,
        1);

    Serial.println(
        " degree Celsius");

    Serial.print(
        "Relative Humidity : ");

    Serial.print(
        relativeHumidity,
        1);

    Serial.println(
        " %RH");

    Serial.print(
        "Air Pressure      : ");

    Serial.print(
        airPressure,
        1);

    Serial.println(
        " hPa");

    Serial.println();
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

    uint32_t requestLength =
        0;

    bool frameRead =
        transport.readFrame(
            requestBuffer,
            sizeof(requestBuffer),
            requestLength);

    if (!frameRead)
    {
        Serial.println(
            "Failed to read TCP frame. Closing client connection.");

        transport.disconnectClient();

        return;
    }

    printPayload(
        "Received",
        requestBuffer,
        requestLength);

    HaseProtocolEnvelope envelope;

    bool envelopeDecoded =
        HaseProtocolEnvelopeCodec::Decode(
            requestBuffer,
            requestLength,
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

        if (processProtocolFrame(
                envelope,
                dispatchResult))
        {
            return;
        }
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
            requestBuffer,
            requestLength);

    if (!frameWritten)
    {
        Serial.println(
            "Failed to echo TCP frame. Closing client connection.");

        transport.disconnectClient();

        return;
    }

    printPayload(
        "Echoed",
        requestBuffer,
        requestLength);
}

bool processProtocolFrame(
    const HaseProtocolEnvelope& envelope,
    HaseProtocolDispatchResult dispatchResult)
{
    switch (dispatchResult)
    {
        case HaseProtocolDispatchResult::
            DiscoverRequestRecognized:
        {
            return processDiscoverRequest(
                envelope);
        }

        case HaseProtocolDispatchResult::
            ReadPropertyRequestRecognized:
        {
            return processReadPropertyRequest(
                envelope);
        }

        case HaseProtocolDispatchResult::
            ReadEndpointDescriptorRequestRecognized:
        {
            return processReadEndpointDescriptorRequest(
                envelope);
        }

        default:
        {
            return false;
        }
    }
}

bool processDiscoverRequest(
    const HaseProtocolEnvelope& envelope)
{
    uint32_t responseLength =
        0;

    bool responseCreated =
        HaseDiscoverHandler::CreateResponse(
            envelope,
            responseBuffer,
            sizeof(responseBuffer),
            responseLength);

    if (!responseCreated)
    {
        Serial.println(
            "Failed to create DiscoverResponse.");

        transport.disconnectClient();

        return true;
    }

    bool responseWritten =
        transport.writeFrame(
            responseBuffer,
            responseLength);

    if (!responseWritten)
    {
        Serial.println(
            "Failed to write DiscoverResponse. "
            "Closing client connection.");

        transport.disconnectClient();

        return true;
    }

    printPayload(
        "Responded",
        responseBuffer,
        responseLength);

    Serial.println();

    Serial.println(
        "DiscoverResponse sent.");

    Serial.print(
        "Endpoint ID   : ");

    Serial.println(
        HaseDiscoverHandler::EndpointId());

    Serial.print(
        "Instrument ID : ");

    Serial.println(
        HaseDiscoverHandler::InstrumentId());

    Serial.println();

    return true;
}

bool processReadPropertyRequest(
    const HaseProtocolEnvelope& envelope)
{
    static_cast<void>(
        envelope);

    Serial.println();

    Serial.println(
        "ReadPropertyRequest recognized.");

    Serial.println(
        "ReadPropertyResponse handler is not implemented yet.");

    Serial.println();

    return true;
}

bool processReadEndpointDescriptorRequest(
    const HaseProtocolEnvelope& envelope)
{
    uint32_t responseLength =
        0;

    const HaseEndpointDescriptor& descriptor =
        HasePhysicalEndpointDescriptor::Descriptor();

    bool responseCreated =
        HaseReadEndpointDescriptorHandler::CreateResponse(
            envelope,
            descriptor,
            responseBuffer,
            sizeof(responseBuffer),
            responseLength);

    if (!responseCreated)
    {
        Serial.println(
            "Failed to create ReadEndpointDescriptorResponse.");

        transport.disconnectClient();

        return true;
    }

    bool responseWritten =
        transport.writeFrame(
            responseBuffer,
            responseLength);

    if (!responseWritten)
    {
        Serial.println(
            "Failed to write ReadEndpointDescriptorResponse. "
            "Closing client connection.");

        transport.disconnectClient();

        return true;
    }

    printPayload(
        "Responded",
        responseBuffer,
        responseLength);

    Serial.println();

    Serial.println(
        "ReadEndpointDescriptorResponse sent.");

    Serial.print(
        "Endpoint ID : ");

    Serial.println(
        descriptor.id);

    Serial.println();

    return true;
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
            ReadPropertyRequestRecognized:
        {
            Serial.println(
                "ReadPropertyRequest recognized");

            break;
        }

        case HaseProtocolDispatchResult::
            ReadEndpointDescriptorRequestRecognized:
        {
            Serial.println(
                "ReadEndpointDescriptorRequest recognized");

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
            InvalidReadPropertyRequest:
        {
            Serial.println(
                "Invalid ReadPropertyRequest");

            break;
        }

        case HaseProtocolDispatchResult::
            InvalidReadEndpointDescriptorRequest:
        {
            Serial.println(
                "Invalid ReadEndpointDescriptorRequest");

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