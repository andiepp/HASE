#include <HaseEsp32Endpoint.h>

#include "EndpointApplication.h"
#include "EndpointConfiguration.h"
#include "HaseSecrets.h"

EndpointApplication endpointApplication;

const HaseEndpointDefinition& endpointDefinition =
    CreateEndpointDefinition(
        endpointApplication);

HaseEndpointRuntime endpointRuntime(
    EndpointConfiguration,
    endpointDefinition,
    endpointApplication);

void setup()
{
    Serial.begin(
        115200);

    delay(
        500);

    Serial.println();
    Serial.println(
        "HASE ESP32 Endpoint");
    Serial.println();

    endpointRuntime.begin(
        WIFI_SSID,
        WIFI_PASSWORD);
}

void loop()
{
    endpointRuntime.update();
}
