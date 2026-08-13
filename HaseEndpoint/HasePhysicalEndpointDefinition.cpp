#include "HasePhysicalEndpointDefinition.h"

#include "HasePhysicalEndpointDescriptor.h"

HasePhysicalEndpointDefinition::HasePhysicalEndpointDefinition(
    HasePhysicalPropertyService& propertyService)
    : _properties{},
      _commands{},
      _events{},
      _definition{}
{
    const HaseEndpointDescriptor& endpoint =
        HasePhysicalEndpointDescriptor::Descriptor();

    const HaseInstrumentDescriptor& environmentInstrument =
        endpoint.instruments[0];

    const HaseInstrumentDescriptor& controllerInstrument =
        endpoint.instruments[1];

    _properties[0] =
    {
        &environmentInstrument,
        &environmentInstrument.properties[0],
        &propertyService,
        readTemperature,
        nullptr,
        nullptr
    };

    _properties[1] =
    {
        &environmentInstrument,
        &environmentInstrument.properties[1],
        &propertyService,
        readRelativeHumidity,
        nullptr,
        nullptr
    };

    _properties[2] =
    {
        &environmentInstrument,
        &environmentInstrument.properties[2],
        &propertyService,
        readAirPressure,
        nullptr,
        nullptr
    };

    _properties[3] =
    {
        &controllerInstrument,
        &controllerInstrument.properties[0],
        &propertyService,
        nullptr,
        readStatusLedEnabled,
        writeStatusLedEnabled
    };

    _commands[0] =
    {
        &controllerInstrument,
        &controllerInstrument.commands[0],
        &propertyService,
        toggleStatusLed
    };

    _events[0] =
    {
        &controllerInstrument,
        &controllerInstrument.events[0]
    };

    _definition =
    {
        &endpoint,
        _properties,
        4,
        _commands,
        1,
        _events,
        1
    };
}

const HaseEndpointDefinition&
    HasePhysicalEndpointDefinition::definition() const
{
    return _definition;
}

const HaseEventRegistration&
    HasePhysicalEndpointDefinition::buttonPressedEvent() const
{
    return _events[0];
}

HaseApplicationResult HasePhysicalEndpointDefinition::readTemperature(
    void* context,
    double& value)
{
    return static_cast<HasePhysicalPropertyService*>(context)->
        readTemperature(value);
}

HaseApplicationResult
    HasePhysicalEndpointDefinition::readRelativeHumidity(
        void* context,
        double& value)
{
    return static_cast<HasePhysicalPropertyService*>(context)->
        readRelativeHumidity(value);
}

HaseApplicationResult HasePhysicalEndpointDefinition::readAirPressure(
    void* context,
    double& value)
{
    return static_cast<HasePhysicalPropertyService*>(context)->
        readAirPressure(value);
}

HaseApplicationResult
    HasePhysicalEndpointDefinition::readStatusLedEnabled(
        void* context,
        bool& value)
{
    return static_cast<HasePhysicalPropertyService*>(context)->
        readStatusLedEnabled(value);
}

HaseApplicationResult
    HasePhysicalEndpointDefinition::writeStatusLedEnabled(
        void* context,
        bool value)
{
    return static_cast<HasePhysicalPropertyService*>(context)->
        writeStatusLedEnabled(value);
}

HaseApplicationResult HasePhysicalEndpointDefinition::toggleStatusLed(
    void* context,
    bool& enabled)
{
    return static_cast<HasePhysicalPropertyService*>(context)->
        toggleStatusLed(enabled);
}
