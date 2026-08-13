#include <HaseEsp32Endpoint.h>

static HaseApplicationResult ReadNumeric(
    void*,
    double& value)
{
    value = 0.0;
    return HaseApplicationResult::Success;
}

static HaseApplicationResult ReadBoolean(
    void*,
    bool& value)
{
    value = false;
    return HaseApplicationResult::Success;
}

static HaseApplicationResult WriteBoolean(
    void*,
    bool)
{
    return HaseApplicationResult::Success;
}

static HaseApplicationResult ExecuteNullBoolean(
    void*,
    bool& returnValue)
{
    returnValue = true;
    return HaseApplicationResult::Success;
}

constexpr HaseNumericDataDescriptor NumericData = {
    "temperature",
    "Temperature",
    "degree-celsius",
    "degree Celsius",
    "C",
    { false, 0.0, 0.0 },
    { false, 0.0 }
};

constexpr HasePropertyDescriptor Properties[] = {
    {
        "temperature",
        "Environment.Temperature",
        "Temperature",
        "Temperature",
        HasePropertyAccessMode::Read,
        HaseDataDescriptorType::Numeric,
        NumericData
    },
    {
        "enabled",
        "Controller.Enabled",
        "Enabled",
        "Enabled",
        HasePropertyAccessMode::ReadWrite,
        HaseDataDescriptorType::Boolean,
        {}
    }
};

constexpr HaseCommandDescriptor Commands[] = {
    {
        "Controller.Toggle",
        "Toggle",
        "Toggle"
    }
};

constexpr HaseEventDescriptor Events[] = {
    {
        "Controller.Pressed",
        "Pressed",
        "Pressed"
    }
};

constexpr HaseInstrumentDescriptor Instruments[] = {
    {
        "instrument-01",
        "Instrument",
        "Validation",
        {},
        Properties,
        2,
        Commands,
        1,
        Events,
        1
    }
};

constexpr HaseEndpointDescriptor Endpoint = {
    "endpoint-01",
    {},
    Instruments,
    1
};

constexpr HasePropertyRegistration ValidProperties[] = {
    {
        &Instruments[0],
        &Properties[0],
        nullptr,
        ReadNumeric,
        nullptr,
        nullptr
    },
    {
        &Instruments[0],
        &Properties[1],
        nullptr,
        nullptr,
        ReadBoolean,
        WriteBoolean
    }
};

constexpr HaseCommandRegistration ValidCommands[] = {
    {
        &Instruments[0],
        &Commands[0],
        nullptr,
        ExecuteNullBoolean
    }
};

constexpr HaseEventRegistration ValidEvents[] = {
    {
        &Instruments[0],
        &Events[0]
    }
};

constexpr HaseEndpointDefinition ValidDefinition = {
    &Endpoint,
    ValidProperties,
    2,
    ValidCommands,
    1,
    ValidEvents,
    1
};

static_assert(
    HaseEndpointDefinitionValidator::Validate(ValidDefinition)
        == HaseEndpointDefinitionValidationResult::Valid,
    "A complete typed endpoint definition must be valid.");

constexpr HaseEndpointDefinition MissingDescriptorDefinition = {
    nullptr,
    nullptr,
    0,
    nullptr,
    0,
    nullptr,
    0
};

static_assert(
    HaseEndpointDefinitionValidator::Validate(MissingDescriptorDefinition)
        == HaseEndpointDefinitionValidationResult::MissingEndpointDescriptor,
    "A missing endpoint descriptor must be rejected.");

constexpr HaseEndpointDefinition MissingPropertyDefinition = {
    &Endpoint,
    ValidProperties,
    1,
    ValidCommands,
    1,
    ValidEvents,
    1
};

static_assert(
    HaseEndpointDefinitionValidator::Validate(MissingPropertyDefinition)
        == HaseEndpointDefinitionValidationResult::MissingPropertyRegistration,
    "A missing Property registration must be rejected.");

constexpr HasePropertyRegistration DuplicateProperties[] = {
    ValidProperties[0],
    ValidProperties[0],
    ValidProperties[1]
};

constexpr HaseEndpointDefinition DuplicatePropertyDefinition = {
    &Endpoint,
    DuplicateProperties,
    3,
    ValidCommands,
    1,
    ValidEvents,
    1
};

static_assert(
    HaseEndpointDefinitionValidator::Validate(DuplicatePropertyDefinition)
        == HaseEndpointDefinitionValidationResult::DuplicatePropertyRegistration,
    "A duplicate Property registration must be rejected.");

constexpr HasePropertyRegistration MissingNumericCallbackProperties[] = {
    {
        &Instruments[0],
        &Properties[0],
        nullptr,
        nullptr,
        nullptr,
        nullptr
    },
    ValidProperties[1]
};

constexpr HaseEndpointDefinition MissingNumericCallbackDefinition = {
    &Endpoint,
    MissingNumericCallbackProperties,
    2,
    ValidCommands,
    1,
    ValidEvents,
    1
};

static_assert(
    HaseEndpointDefinitionValidator::Validate(
        MissingNumericCallbackDefinition)
        == HaseEndpointDefinitionValidationResult::PropertyCallbackMismatch,
    "A readable Numeric Property requires its typed callback.");

constexpr HasePropertyRegistration WrongBooleanCallbackProperties[] = {
    ValidProperties[0],
    {
        &Instruments[0],
        &Properties[1],
        nullptr,
        ReadNumeric,
        ReadBoolean,
        WriteBoolean
    }
};

constexpr HaseEndpointDefinition WrongBooleanCallbackDefinition = {
    &Endpoint,
    WrongBooleanCallbackProperties,
    2,
    ValidCommands,
    1,
    ValidEvents,
    1
};

static_assert(
    HaseEndpointDefinitionValidator::Validate(WrongBooleanCallbackDefinition)
        == HaseEndpointDefinitionValidationResult::PropertyCallbackMismatch,
    "A Boolean Property must reject a Numeric callback.");

constexpr HaseCommandRegistration MissingCommandCallback[] = {
    {
        &Instruments[0],
        &Commands[0],
        nullptr,
        nullptr
    }
};

constexpr HaseEndpointDefinition MissingCommandCallbackDefinition = {
    &Endpoint,
    ValidProperties,
    2,
    MissingCommandCallback,
    1,
    ValidEvents,
    1
};

static_assert(
    HaseEndpointDefinitionValidator::Validate(
        MissingCommandCallbackDefinition)
        == HaseEndpointDefinitionValidationResult::CommandCallbackMismatch,
    "A Command requires its typed callback.");

constexpr HasePropertyDescriptor ForeignProperty = Properties[0];

constexpr HasePropertyRegistration ForeignProperties[] = {
    {
        &Instruments[0],
        &ForeignProperty,
        nullptr,
        ReadNumeric,
        nullptr,
        nullptr
    },
    ValidProperties[1]
};

constexpr HaseEndpointDefinition ForeignPropertyDefinition = {
    &Endpoint,
    ForeignProperties,
    2,
    ValidCommands,
    1,
    ValidEvents,
    1
};

static_assert(
    HaseEndpointDefinitionValidator::Validate(ForeignPropertyDefinition)
        == HaseEndpointDefinitionValidationResult::ForeignPropertyRegistration,
    "A descriptor copy must not satisfy identity-based registration.");

constexpr HaseEndpointDefinition MissingEventDefinition = {
    &Endpoint,
    ValidProperties,
    2,
    ValidCommands,
    1,
    nullptr,
    0
};

static_assert(
    HaseEndpointDefinitionValidator::Validate(MissingEventDefinition)
        == HaseEndpointDefinitionValidationResult::MissingEventRegistration,
    "A published Event requires exactly one registration.");

void setup()
{
}

void loop()
{
}
