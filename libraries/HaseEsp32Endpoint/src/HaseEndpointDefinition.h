#pragma once

#include <Arduino.h>

#include "HaseApplicationCallbacks.h"
#include "HaseDescriptorModel.h"

struct HasePropertyRegistration
{
    const HaseInstrumentDescriptor* instrument;
    const HasePropertyDescriptor* property;

    void* context;

    HaseReadNumericPropertyCallback readNumeric;
    HaseReadBooleanPropertyCallback readBoolean;
    HaseWriteBooleanPropertyCallback writeBoolean;
};

struct HaseCommandRegistration
{
    const HaseInstrumentDescriptor* instrument;
    const HaseCommandDescriptor* command;

    void* context;

    HaseExecuteNullBooleanCommandCallback executeNullBoolean;
};

struct HaseEventRegistration
{
    const HaseInstrumentDescriptor* instrument;
    const HaseEventDescriptor* event;
};

struct HaseEndpointDefinition
{
    const HaseEndpointDescriptor* descriptor;

    const HasePropertyRegistration* properties;
    uint16_t propertyCount;

    const HaseCommandRegistration* commands;
    uint16_t commandCount;

    const HaseEventRegistration* events;
    uint16_t eventCount;
};
