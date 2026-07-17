#pragma once

#include <Arduino.h>

// -----------------------------------------------------------------------------
// Endpoint metadata
// -----------------------------------------------------------------------------

struct HaseEndpointMetadata
{
    const char* displayName;
    const char* description;
};

// -----------------------------------------------------------------------------
// Instrument metadata
// -----------------------------------------------------------------------------

struct HaseInstrumentMetadata
{
    const char* manufacturer;
    const char* model;
    const char* serialNumber;
    const char* firmwareVersion;
    const char* hardwareRevision;
    const char* description;
};

// -----------------------------------------------------------------------------
// Numeric data descriptor
// -----------------------------------------------------------------------------

struct HaseOptionalValueRange
{
    bool hasValue;

    double minimum;
    double maximum;
};

struct HaseOptionalResolution
{
    bool hasValue;

    double value;
};

struct HaseNumericDataDescriptor
{
    const char* quantityId;
    const char* quantityDisplayName;

    const char* unitId;
    const char* unitDisplayName;
    const char* unitSymbol;

    HaseOptionalValueRange range;
    HaseOptionalResolution resolution;
};

// -----------------------------------------------------------------------------
// Property descriptor
// -----------------------------------------------------------------------------

enum class HasePropertyAccessMode : uint8_t
{
    None =
        0,

    Read =
        1,

    Write =
        2,

    ReadWrite =
        3
};

enum class HaseDataDescriptorType : uint8_t
{
    String =
        1,

    Numeric =
        2,

    Boolean =
        3
};

struct HasePropertyDescriptor
{
    const char* id;
    const char* path;
    const char* displayName;
    const char* description;

    HasePropertyAccessMode accessMode;
    HaseDataDescriptorType dataType;

    HaseNumericDataDescriptor numericData;
};

// -----------------------------------------------------------------------------
// Command descriptor
// -----------------------------------------------------------------------------

struct HaseCommandDescriptor
{
    const char* path;
    const char* displayName;
    const char* description;
};

// -----------------------------------------------------------------------------
// Instrument descriptor
// -----------------------------------------------------------------------------

struct HaseInstrumentDescriptor
{
    const char* id;
    const char* name;
    const char* kind;

    HaseInstrumentMetadata metadata;

    const HasePropertyDescriptor* properties;
    uint16_t propertyCount;

    const HaseCommandDescriptor* commands;
    uint16_t commandCount;

    uint16_t eventCount;
};

// -----------------------------------------------------------------------------
// Endpoint descriptor
// -----------------------------------------------------------------------------

struct HaseEndpointDescriptor
{
    const char* id;

    HaseEndpointMetadata metadata;

    const HaseInstrumentDescriptor* instruments;
    uint16_t instrumentCount;
};