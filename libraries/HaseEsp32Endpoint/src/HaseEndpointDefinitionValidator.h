#pragma once

#include <Arduino.h>

#include "HaseEndpointDefinition.h"

enum class HaseEndpointDefinitionValidationResult : uint8_t
{
    Valid =
        0,

    MissingEndpointDescriptor,
    InvalidEndpointIdentity,
    InvalidDescriptorCollection,
    InvalidInstrumentIdentity,
    DuplicateInstrumentIdentity,
    InvalidPropertyIdentity,
    DuplicatePropertyIdentity,
    InvalidCommandIdentity,
    DuplicateCommandIdentity,
    InvalidEventIdentity,
    DuplicateEventIdentity,
    InvalidRegistrationCollection,
    ForeignPropertyRegistration,
    ForeignCommandRegistration,
    ForeignEventRegistration,
    MissingPropertyRegistration,
    DuplicatePropertyRegistration,
    MissingCommandRegistration,
    DuplicateCommandRegistration,
    MissingEventRegistration,
    DuplicateEventRegistration,
    UnsupportedPropertyContract,
    PropertyCallbackMismatch,
    CommandCallbackMismatch
};

class HaseEndpointDefinitionValidator
{
public:
    static constexpr HaseEndpointDefinitionValidationResult Validate(
        const HaseEndpointDefinition& definition)
    {
        if (definition.descriptor == nullptr)
        {
            return HaseEndpointDefinitionValidationResult::
                MissingEndpointDescriptor;
        }

        if (!IsNonEmpty(definition.descriptor->id))
        {
            return HaseEndpointDefinitionValidationResult::
                InvalidEndpointIdentity;
        }

        if (!IsCollectionValid(
                definition.descriptor->instruments,
                definition.descriptor->instrumentCount))
        {
            return HaseEndpointDefinitionValidationResult::
                InvalidDescriptorCollection;
        }

        const HaseEndpointDefinitionValidationResult descriptorResult =
            ValidateDescriptors(*definition.descriptor);

        if (descriptorResult !=
            HaseEndpointDefinitionValidationResult::Valid)
        {
            return descriptorResult;
        }

        if (!IsCollectionValid(
                definition.properties,
                definition.propertyCount)
            || !IsCollectionValid(
                definition.commands,
                definition.commandCount)
            || !IsCollectionValid(
                definition.events,
                definition.eventCount))
        {
            return HaseEndpointDefinitionValidationResult::
                InvalidRegistrationCollection;
        }

        const HaseEndpointDefinitionValidationResult registrationResult =
            ValidateRegistrationTargets(definition);

        if (registrationResult !=
            HaseEndpointDefinitionValidationResult::Valid)
        {
            return registrationResult;
        }

        return ValidateRegistrationCoverage(definition);
    }

private:
    template<typename T>
    static constexpr bool IsCollectionValid(
        const T* values,
        uint16_t count)
    {
        return (count == 0 && values == nullptr)
            || (count > 0 && values != nullptr);
    }

    static constexpr bool IsNonEmpty(
        const char* value)
    {
        return value != nullptr && value[0] != '\0';
    }

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

    static constexpr HaseEndpointDefinitionValidationResult
        ValidateDescriptors(
            const HaseEndpointDescriptor& endpoint)
    {
        for (uint16_t instrumentIndex = 0;
            instrumentIndex < endpoint.instrumentCount;
            instrumentIndex++)
        {
            const HaseInstrumentDescriptor& instrument =
                endpoint.instruments[instrumentIndex];

            if (!IsNonEmpty(instrument.id))
            {
                return HaseEndpointDefinitionValidationResult::
                    InvalidInstrumentIdentity;
            }

            for (uint16_t comparisonIndex = 0;
                comparisonIndex < instrumentIndex;
                comparisonIndex++)
            {
                if (StringEquals(
                        instrument.id,
                        endpoint.instruments[comparisonIndex].id))
                {
                    return HaseEndpointDefinitionValidationResult::
                        DuplicateInstrumentIdentity;
                }
            }

            if (!IsCollectionValid(
                    instrument.properties,
                    instrument.propertyCount)
                || !IsCollectionValid(
                    instrument.commands,
                    instrument.commandCount)
                || !IsCollectionValid(
                    instrument.events,
                    instrument.eventCount))
            {
                return HaseEndpointDefinitionValidationResult::
                    InvalidDescriptorCollection;
            }

            const HaseEndpointDefinitionValidationResult memberResult =
                ValidateInstrumentMembers(instrument);

            if (memberResult !=
                HaseEndpointDefinitionValidationResult::Valid)
            {
                return memberResult;
            }
        }

        return HaseEndpointDefinitionValidationResult::Valid;
    }

    static constexpr HaseEndpointDefinitionValidationResult
        ValidateInstrumentMembers(
            const HaseInstrumentDescriptor& instrument)
    {
        for (uint16_t propertyIndex = 0;
            propertyIndex < instrument.propertyCount;
            propertyIndex++)
        {
            const HasePropertyDescriptor& property =
                instrument.properties[propertyIndex];

            if (!IsNonEmpty(property.id) || !IsNonEmpty(property.path))
            {
                return HaseEndpointDefinitionValidationResult::
                    InvalidPropertyIdentity;
            }

            for (uint16_t comparisonIndex = 0;
                comparisonIndex < propertyIndex;
                comparisonIndex++)
            {
                const HasePropertyDescriptor& comparison =
                    instrument.properties[comparisonIndex];

                if (StringEquals(property.id, comparison.id)
                    || StringEquals(property.path, comparison.path))
                {
                    return HaseEndpointDefinitionValidationResult::
                        DuplicatePropertyIdentity;
                }
            }
        }

        for (uint16_t commandIndex = 0;
            commandIndex < instrument.commandCount;
            commandIndex++)
        {
            const HaseCommandDescriptor& command =
                instrument.commands[commandIndex];

            if (!IsNonEmpty(command.path))
            {
                return HaseEndpointDefinitionValidationResult::
                    InvalidCommandIdentity;
            }

            for (uint16_t comparisonIndex = 0;
                comparisonIndex < commandIndex;
                comparisonIndex++)
            {
                if (StringEquals(
                        command.path,
                        instrument.commands[comparisonIndex].path))
                {
                    return HaseEndpointDefinitionValidationResult::
                        DuplicateCommandIdentity;
                }
            }
        }

        for (uint16_t eventIndex = 0;
            eventIndex < instrument.eventCount;
            eventIndex++)
        {
            const HaseEventDescriptor& event =
                instrument.events[eventIndex];

            if (!IsNonEmpty(event.path))
            {
                return HaseEndpointDefinitionValidationResult::
                    InvalidEventIdentity;
            }

            for (uint16_t comparisonIndex = 0;
                comparisonIndex < eventIndex;
                comparisonIndex++)
            {
                if (StringEquals(
                        event.path,
                        instrument.events[comparisonIndex].path))
                {
                    return HaseEndpointDefinitionValidationResult::
                        DuplicateEventIdentity;
                }
            }
        }

        return HaseEndpointDefinitionValidationResult::Valid;
    }

    static constexpr HaseEndpointDefinitionValidationResult
        ValidateRegistrationTargets(
            const HaseEndpointDefinition& definition)
    {
        for (uint16_t index = 0;
            index < definition.propertyCount;
            index++)
        {
            const HasePropertyRegistration& registration =
                definition.properties[index];

            if (!ContainsProperty(
                    *definition.descriptor,
                    registration.instrument,
                    registration.property))
            {
                return HaseEndpointDefinitionValidationResult::
                    ForeignPropertyRegistration;
            }

            const HaseEndpointDefinitionValidationResult callbackResult =
                ValidatePropertyCallbacks(registration);

            if (callbackResult !=
                HaseEndpointDefinitionValidationResult::Valid)
            {
                return callbackResult;
            }
        }

        for (uint16_t index = 0;
            index < definition.commandCount;
            index++)
        {
            const HaseCommandRegistration& registration =
                definition.commands[index];

            if (!ContainsCommand(
                    *definition.descriptor,
                    registration.instrument,
                    registration.command))
            {
                return HaseEndpointDefinitionValidationResult::
                    ForeignCommandRegistration;
            }

            if (registration.executeNullBoolean == nullptr)
            {
                return HaseEndpointDefinitionValidationResult::
                    CommandCallbackMismatch;
            }
        }

        for (uint16_t index = 0;
            index < definition.eventCount;
            index++)
        {
            const HaseEventRegistration& registration =
                definition.events[index];

            if (!ContainsEvent(
                    *definition.descriptor,
                    registration.instrument,
                    registration.event))
            {
                return HaseEndpointDefinitionValidationResult::
                    ForeignEventRegistration;
            }
        }

        return HaseEndpointDefinitionValidationResult::Valid;
    }

    static constexpr HaseEndpointDefinitionValidationResult
        ValidatePropertyCallbacks(
            const HasePropertyRegistration& registration)
    {
        const bool canRead =
            registration.property->accessMode == HasePropertyAccessMode::Read
            || registration.property->accessMode ==
                HasePropertyAccessMode::ReadWrite;

        const bool canWrite =
            registration.property->accessMode == HasePropertyAccessMode::Write
            || registration.property->accessMode ==
                HasePropertyAccessMode::ReadWrite;

        if (registration.property->dataType ==
            HaseDataDescriptorType::Numeric)
        {
            if (canWrite)
            {
                return HaseEndpointDefinitionValidationResult::
                    UnsupportedPropertyContract;
            }

            if ((canRead && registration.readNumeric == nullptr)
                || (!canRead && registration.readNumeric != nullptr)
                || registration.readBoolean != nullptr
                || registration.writeBoolean != nullptr)
            {
                return HaseEndpointDefinitionValidationResult::
                    PropertyCallbackMismatch;
            }

            return HaseEndpointDefinitionValidationResult::Valid;
        }

        if (registration.property->dataType ==
            HaseDataDescriptorType::Boolean)
        {
            if ((canRead && registration.readBoolean == nullptr)
                || (!canRead && registration.readBoolean != nullptr)
                || (canWrite && registration.writeBoolean == nullptr)
                || (!canWrite && registration.writeBoolean != nullptr)
                || registration.readNumeric != nullptr)
            {
                return HaseEndpointDefinitionValidationResult::
                    PropertyCallbackMismatch;
            }

            return HaseEndpointDefinitionValidationResult::Valid;
        }

        return HaseEndpointDefinitionValidationResult::
            UnsupportedPropertyContract;
    }

    static constexpr HaseEndpointDefinitionValidationResult
        ValidateRegistrationCoverage(
            const HaseEndpointDefinition& definition)
    {
        for (uint16_t instrumentIndex = 0;
            instrumentIndex < definition.descriptor->instrumentCount;
            instrumentIndex++)
        {
            const HaseInstrumentDescriptor& instrument =
                definition.descriptor->instruments[instrumentIndex];

            for (uint16_t propertyIndex = 0;
                propertyIndex < instrument.propertyCount;
                propertyIndex++)
            {
                const uint16_t count = CountPropertyRegistrations(
                    definition,
                    &instrument,
                    &instrument.properties[propertyIndex]);

                if (count == 0)
                {
                    return HaseEndpointDefinitionValidationResult::
                        MissingPropertyRegistration;
                }

                if (count > 1)
                {
                    return HaseEndpointDefinitionValidationResult::
                        DuplicatePropertyRegistration;
                }
            }

            for (uint16_t commandIndex = 0;
                commandIndex < instrument.commandCount;
                commandIndex++)
            {
                const uint16_t count = CountCommandRegistrations(
                    definition,
                    &instrument,
                    &instrument.commands[commandIndex]);

                if (count == 0)
                {
                    return HaseEndpointDefinitionValidationResult::
                        MissingCommandRegistration;
                }

                if (count > 1)
                {
                    return HaseEndpointDefinitionValidationResult::
                        DuplicateCommandRegistration;
                }
            }

            for (uint16_t eventIndex = 0;
                eventIndex < instrument.eventCount;
                eventIndex++)
            {
                const uint16_t count = CountEventRegistrations(
                    definition,
                    &instrument,
                    &instrument.events[eventIndex]);

                if (count == 0)
                {
                    return HaseEndpointDefinitionValidationResult::
                        MissingEventRegistration;
                }

                if (count > 1)
                {
                    return HaseEndpointDefinitionValidationResult::
                        DuplicateEventRegistration;
                }
            }
        }

        return HaseEndpointDefinitionValidationResult::Valid;
    }

    static constexpr bool ContainsProperty(
        const HaseEndpointDescriptor& endpoint,
        const HaseInstrumentDescriptor* instrument,
        const HasePropertyDescriptor* property)
    {
        for (uint16_t instrumentIndex = 0;
            instrumentIndex < endpoint.instrumentCount;
            instrumentIndex++)
        {
            if (&endpoint.instruments[instrumentIndex] != instrument)
            {
                continue;
            }

            for (uint16_t propertyIndex = 0;
                propertyIndex < instrument->propertyCount;
                propertyIndex++)
            {
                if (&instrument->properties[propertyIndex] == property)
                {
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    static constexpr bool ContainsCommand(
        const HaseEndpointDescriptor& endpoint,
        const HaseInstrumentDescriptor* instrument,
        const HaseCommandDescriptor* command)
    {
        for (uint16_t instrumentIndex = 0;
            instrumentIndex < endpoint.instrumentCount;
            instrumentIndex++)
        {
            if (&endpoint.instruments[instrumentIndex] != instrument)
            {
                continue;
            }

            for (uint16_t commandIndex = 0;
                commandIndex < instrument->commandCount;
                commandIndex++)
            {
                if (&instrument->commands[commandIndex] == command)
                {
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    static constexpr bool ContainsEvent(
        const HaseEndpointDescriptor& endpoint,
        const HaseInstrumentDescriptor* instrument,
        const HaseEventDescriptor* event)
    {
        for (uint16_t instrumentIndex = 0;
            instrumentIndex < endpoint.instrumentCount;
            instrumentIndex++)
        {
            if (&endpoint.instruments[instrumentIndex] != instrument)
            {
                continue;
            }

            for (uint16_t eventIndex = 0;
                eventIndex < instrument->eventCount;
                eventIndex++)
            {
                if (&instrument->events[eventIndex] == event)
                {
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    static constexpr uint16_t CountPropertyRegistrations(
        const HaseEndpointDefinition& definition,
        const HaseInstrumentDescriptor* instrument,
        const HasePropertyDescriptor* property)
    {
        uint16_t count = 0;

        for (uint16_t index = 0;
            index < definition.propertyCount;
            index++)
        {
            if (definition.properties[index].instrument == instrument
                && definition.properties[index].property == property)
            {
                count++;
            }
        }

        return count;
    }

    static constexpr uint16_t CountCommandRegistrations(
        const HaseEndpointDefinition& definition,
        const HaseInstrumentDescriptor* instrument,
        const HaseCommandDescriptor* command)
    {
        uint16_t count = 0;

        for (uint16_t index = 0;
            index < definition.commandCount;
            index++)
        {
            if (definition.commands[index].instrument == instrument
                && definition.commands[index].command == command)
            {
                count++;
            }
        }

        return count;
    }

    static constexpr uint16_t CountEventRegistrations(
        const HaseEndpointDefinition& definition,
        const HaseInstrumentDescriptor* instrument,
        const HaseEventDescriptor* event)
    {
        uint16_t count = 0;

        for (uint16_t index = 0;
            index < definition.eventCount;
            index++)
        {
            if (definition.events[index].instrument == instrument
                && definition.events[index].event == event)
            {
                count++;
            }
        }

        return count;
    }

    HaseEndpointDefinitionValidator() =
        delete;
};
