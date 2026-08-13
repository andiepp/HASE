#pragma once

#include <Arduino.h>

#include "HaseEndpointDefinition.h"

class HaseEndpointDefinitionResolver
{
public:
    static constexpr const HaseInstrumentDescriptor* ResolveInstrument(
        const HaseEndpointDefinition& definition,
        const char* instrumentId)
    {
        if (definition.descriptor == nullptr || instrumentId == nullptr)
        {
            return nullptr;
        }

        for (uint16_t index = 0;
            index < definition.descriptor->instrumentCount;
            index++)
        {
            const HaseInstrumentDescriptor* instrument =
                &definition.descriptor->instruments[index];

            if (StringEquals(instrument->id, instrumentId))
            {
                return instrument;
            }
        }

        return nullptr;
    }

    static constexpr const HasePropertyRegistration* ResolveProperty(
        const HaseEndpointDefinition& definition,
        const char* instrumentId,
        const char* propertyId)
    {
        const HaseInstrumentDescriptor* instrument =
            ResolveInstrument(definition, instrumentId);

        if (instrument == nullptr || propertyId == nullptr)
        {
            return nullptr;
        }

        for (uint16_t index = 0;
            index < definition.propertyCount;
            index++)
        {
            const HasePropertyRegistration* registration =
                &definition.properties[index];

            if (registration->instrument == instrument
                && registration->property != nullptr
                && StringEquals(registration->property->id, propertyId))
            {
                return registration;
            }
        }

        return nullptr;
    }

    static constexpr const HaseCommandRegistration* ResolveCommand(
        const HaseEndpointDefinition& definition,
        const char* instrumentId,
        const char* commandPath)
    {
        const HaseInstrumentDescriptor* instrument =
            ResolveInstrument(definition, instrumentId);

        if (instrument == nullptr || commandPath == nullptr)
        {
            return nullptr;
        }

        for (uint16_t index = 0;
            index < definition.commandCount;
            index++)
        {
            const HaseCommandRegistration* registration =
                &definition.commands[index];

            if (registration->instrument == instrument
                && registration->command != nullptr
                && StringEquals(registration->command->path, commandPath))
            {
                return registration;
            }
        }

        return nullptr;
    }

    static constexpr const HaseEventRegistration* ResolveEvent(
        const HaseEndpointDefinition& definition,
        const char* instrumentId,
        const char* eventPath)
    {
        const HaseInstrumentDescriptor* instrument =
            ResolveInstrument(definition, instrumentId);

        if (instrument == nullptr || eventPath == nullptr)
        {
            return nullptr;
        }

        for (uint16_t index = 0;
            index < definition.eventCount;
            index++)
        {
            const HaseEventRegistration* registration =
                &definition.events[index];

            if (registration->instrument == instrument
                && registration->event != nullptr
                && StringEquals(registration->event->path, eventPath))
            {
                return registration;
            }
        }

        return nullptr;
    }

private:
    static constexpr bool StringEquals(
        const char* left,
        const char* right)
    {
        if (left == nullptr || right == nullptr)
        {
            return left == right;
        }

        size_t index = 0;

        while (left[index] != '\0' && right[index] != '\0')
        {
            if (left[index] != right[index])
            {
                return false;
            }

            index++;
        }

        return left[index] == right[index];
    }

    HaseEndpointDefinitionResolver() =
        delete;
};
