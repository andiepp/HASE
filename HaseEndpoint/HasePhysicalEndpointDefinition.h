#pragma once

#include <HaseEsp32Endpoint.h>

#include "HasePhysicalPropertyService.h"

class HasePhysicalEndpointDefinition
{
public:
    explicit HasePhysicalEndpointDefinition(
        HasePhysicalPropertyService& propertyService);

    const HaseEndpointDefinition& definition() const;

    const HaseEventRegistration& buttonPressedEvent() const;

private:
    static HaseApplicationResult readTemperature(
        void* context,
        double& value);

    static HaseApplicationResult readRelativeHumidity(
        void* context,
        double& value);

    static HaseApplicationResult readAirPressure(
        void* context,
        double& value);

    static HaseApplicationResult readStatusLedEnabled(
        void* context,
        bool& value);

    static HaseApplicationResult writeStatusLedEnabled(
        void* context,
        bool value);

    static HaseApplicationResult toggleStatusLed(
        void* context,
        bool& enabled);

    HasePropertyRegistration _properties[4];

    HaseCommandRegistration _commands[1];

    HaseEventRegistration _events[1];

    HaseEndpointDefinition _definition;
};
