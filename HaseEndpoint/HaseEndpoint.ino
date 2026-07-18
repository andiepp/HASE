// -----------------------------------------------------------------------------
// HASE Endpoint ESP32
// -----------------------------------------------------------------------------

#include <WiFi.h>

#include "HaseBme280Sensor.h"
#include "HaseDiscoverHandler.h"
#include "HaseMdnsAdvertiser.h"
#include "HasePhysicalEndpointDescriptor.h"
#include "HasePhysicalEventPublisher.h"
#include "HasePhysicalExecuteCommandHandler.h"
#include "HasePhysicalPropertyService.h"
#include "HasePhysicalReadPropertyHandler.h"
#include "HasePhysicalWritePropertyHandler.h"
#include "HaseProtocolDispatcher.h"
#include "HaseProtocolEnvelope.h"
#include "HaseReadEndpointDescriptorHandler.h"
#include "HaseReadPropertyRequest.h"
#include "HaseReadPropertyResponseHandler.h"
#include "HaseSecrets.h"
#include "HaseTcpTransport.h"
#include "HaseUtcClock.h"

// -----------------------------------------------------------------------------
// HASE transport configuration
// -----------------------------------------------------------------------------

constexpr uint16_t TCP_PORT =
    5000;

constexpr const char* MDNS_HOST_NAME =
    "doit-esp32-devkitc-v4-01";

constexpr const char* MDNS_INSTANCE_NAME =
    "doit-esp32-devkitc-v4-01";

constexpr uint32_t MAXIMUM_PAYLOAD_LENGTH =
    4096;

constexpr unsigned long READ_TIMEOUT_MILLISECONDS =
    5000;

constexpr unsigned long UTC_SYNCHRONIZATION_TIMEOUT_MILLISECONDS =
    15000;

// -----------------------------------------------------------------------------
// Global objects and buffers
// -----------------------------------------------------------------------------

HaseBme280Sensor environmentSensor;

HaseMdnsAdvertiser mdnsAdvertiser;

HasePhysicalPropertyService physicalPropertyService(
    environmentSensor);

HaseUtcClock utcClock;

HaseTcpTransport transport(
    TCP_PORT,
    MAXIMUM_PAYLOAD_LENGTH,
    READ_TIMEOUT_MILLISECONDS);

HasePhysicalEventPublisher eventPublisher(
    transport,
    utcClock);

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

bool synchronizeUtcClock();

void startNetworkEndpoint();

void stopNetworkEndpoint();

void processTransport();

bool processProtocolFrame(
    const HaseProtocolEnvelope& envelope,
    HaseProtocolDispatchResult dispatchResult);

bool processDiscoverRequest(
    const HaseProtocolEnvelope& envelope);

bool processReadPropertyRequest(
    const HaseProtocolEnvelope& envelope);

bool processWritePropertyRequest(
    const HaseProtocolEnvelope& envelope);

bool processExecuteCommandRequest(
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

    Serial.println(
        "Firmware capabilities: C-010 ExecuteCommand, EventNotification");

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

    if (!synchronizeUtcClock())
    {
        Serial.println(
            "Endpoint startup stopped because UTC "
            "could not be synchronized.");

        Serial.println();

        return;
    }

    eventPublisher.begin();

    startNetworkEndpoint();

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
        stopNetworkEndpoint();

        connectToWiFi();

        if (!synchronizeUtcClock())
        {
            Serial.println(
                "UTC synchronization failed after Wi-Fi reconnect.");

            Serial.println(
                "Endpoint transport remains stopped.");

            Serial.println();

            endpointStarted =
                false;

            return;
        }

        startNetworkEndpoint();

        Serial.println(
            "Waiting for HASE client...");
    }

    transport.update();

    eventPublisher.update();

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
// Wi-Fi and UTC
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

    Serial.println();
}

bool synchronizeUtcClock()
{
    Serial.println(
        "Synchronizing UTC clock...");

    if (!utcClock.synchronize(
            UTC_SYNCHRONIZATION_TIMEOUT_MILLISECONDS))
    {
        Serial.println(
            "UTC synchronization failed.");

        Serial.println();

        return false;
    }

    int64_t unixTimeMilliseconds =
        0;

    if (!utcClock.tryGetUnixTimeMilliseconds(
            unixTimeMilliseconds))
    {
        Serial.println(
            "UTC clock synchronized but no valid timestamp "
            "could be read.");

        Serial.println();

        return false;
    }

    Serial.println(
        "UTC clock synchronized.");

    Serial.print(
        "Unix time milliseconds : ");

    Serial.println(
        static_cast<long long>(
            unixTimeMilliseconds));

    Serial.println();

    return true;
}

void startNetworkEndpoint()
{
    transport.begin();

    bool advertisementStarted =
        mdnsAdvertiser.begin(
            MDNS_HOST_NAME,
            MDNS_INSTANCE_NAME,
            TCP_PORT);

    if (!advertisementStarted)
    {
        Serial.println(
            "Failed to advertise the HASE TCP endpoint through mDNS.");

        Serial.println();

        return;
    }

    Serial.println(
        "HASE network endpoint advertised through mDNS/DNS-SD.");

    Serial.print(
        "Service instance : ");

    Serial.println(
        MDNS_INSTANCE_NAME);

    Serial.print(
        "Service type     : _hase._tcp.local");

    Serial.println();

    Serial.print(
        "Service port     : ");

    Serial.println(
        TCP_PORT);

    Serial.println();
}

void stopNetworkEndpoint()
{
    mdnsAdvertiser.end();

    transport.disconnectClient();

    Serial.println(
        "HASE network endpoint advertisement stopped.");

    Serial.println();
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
            WritePropertyRequestRecognized:

        case HaseProtocolDispatchResult::
            InvalidWritePropertyRequest:
        {
            return processWritePropertyRequest(
                envelope);
        }

        case HaseProtocolDispatchResult::
            ExecuteCommandRequestRecognized:

        case HaseProtocolDispatchResult::
            InvalidExecuteCommandRequest:
        {
            return processExecuteCommandRequest(
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
    HaseReadPropertyRequest request;

    if (!HaseReadPropertyRequestDecoder::Decode(
            envelope,
            request))
    {
        uint32_t responseLength =
            0;

        HaseReadPropertyResponseHandler::
            CreateFailureResponse(
                envelope,
                HaseReadPropertyResponseHandler::
                    InvalidRequestResultCode,
                "Invalid request",
                responseBuffer,
                sizeof(responseBuffer),
                responseLength);

        transport.writeFrame(
            responseBuffer,
            responseLength);

        return true;
    }

    uint32_t responseLength =
        0;

    bool responseCreated =
        HasePhysicalReadPropertyHandler::CreateResponse(
            envelope,
            request,
            physicalPropertyService,
            utcClock,
            responseBuffer,
            sizeof(responseBuffer),
            responseLength);

    if (!responseCreated)
    {
        Serial.println(
            "Failed to create ReadPropertyResponse.");

        transport.disconnectClient();

        return true;
    }

    if (!transport.writeFrame(
            responseBuffer,
            responseLength))
    {
        Serial.println(
            "Failed to write ReadPropertyResponse. "
            "Closing client connection.");

        transport.disconnectClient();
    }

    return true;
}

bool processWritePropertyRequest(
    const HaseProtocolEnvelope& envelope)
{
    uint32_t responseLength =
        0;

    bool responseCreated =
        HasePhysicalWritePropertyHandler::CreateResponse(
            envelope,
            physicalPropertyService,
            utcClock,
            responseBuffer,
            sizeof(responseBuffer),
            responseLength);

    if (!responseCreated)
    {
        Serial.println(
            "Failed to create WritePropertyResponse.");

        transport.disconnectClient();

        return true;
    }

    if (!transport.writeFrame(
            responseBuffer,
            responseLength))
    {
        Serial.println(
            "Failed to write WritePropertyResponse. "
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
        "WritePropertyResponse sent.");

    Serial.println();

    return true;
}

bool processExecuteCommandRequest(
    const HaseProtocolEnvelope& envelope)
{
    uint32_t responseLength =
        0;

    bool responseCreated =
        HasePhysicalExecuteCommandHandler::CreateResponse(
            envelope,
            physicalPropertyService,
            responseBuffer,
            sizeof(responseBuffer),
            responseLength);

    if (!responseCreated)
    {
        Serial.println(
            "Failed to create ExecuteCommandResponse.");

        transport.disconnectClient();

        return true;
    }

    if (!transport.writeFrame(
            responseBuffer,
            responseLength))
    {
        Serial.println(
            "Failed to write ExecuteCommandResponse. "
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
        "ExecuteCommandResponse sent.");

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

    Serial.print(
        static_cast<uint8_t>(
            result));

    Serial.print(
        " - ");

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
            WritePropertyRequestRecognized:
        {
            Serial.println(
                "WritePropertyRequest recognized");

            break;
        }

        case HaseProtocolDispatchResult::
            ExecuteCommandRequestRecognized:
        {
            Serial.println(
                "ExecuteCommandRequest recognized");

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
            InvalidWritePropertyRequest:
        {
            Serial.println(
                "Invalid WritePropertyRequest");

            break;
        }

        case HaseProtocolDispatchResult::
            InvalidExecuteCommandRequest:
        {
            Serial.println(
                "Invalid ExecuteCommandRequest");

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
