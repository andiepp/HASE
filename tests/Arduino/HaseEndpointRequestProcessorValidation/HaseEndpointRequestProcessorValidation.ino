#include <HaseEsp32Endpoint.h>

struct InvocationState
{
    uint8_t numericReads;
    uint8_t booleanReads;
    uint8_t booleanWrites;
    uint8_t commandExecutions;
};

static HaseApplicationResult ReadNumeric(
    void* context,
    double& value)
{
    InvocationState* state =
        static_cast<InvocationState*>(context);

    state->numericReads++;
    value = 21.5;

    return HaseApplicationResult::Success;
}

static HaseApplicationResult ReadBoolean(
    void* context,
    bool& value)
{
    InvocationState* state =
        static_cast<InvocationState*>(context);

    state->booleanReads++;
    value = true;

    return HaseApplicationResult::Success;
}

static HaseApplicationResult WriteBoolean(
    void* context,
    bool)
{
    InvocationState* state =
        static_cast<InvocationState*>(context);

    state->booleanWrites++;

    return HaseApplicationResult::Success;
}

static HaseApplicationResult ExecuteNullBoolean(
    void* context,
    bool& returnValue)
{
    InvocationState* state =
        static_cast<InvocationState*>(context);

    state->commandExecutions++;
    returnValue = true;

    return HaseApplicationResult::Success;
}

constexpr HaseNumericDataDescriptor NumericData = {
    "temperature",
    "Temperature",
    "degree-celsius",
    "Degree Celsius",
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

InvocationState State = {};

const HasePropertyRegistration PropertyRegistrations[] = {
    {
        &Instruments[0],
        &Properties[0],
        &State,
        ReadNumeric,
        nullptr,
        nullptr
    },
    {
        &Instruments[0],
        &Properties[1],
        &State,
        nullptr,
        ReadBoolean,
        WriteBoolean
    }
};

const HaseCommandRegistration CommandRegistrations[] = {
    {
        &Instruments[0],
        &Commands[0],
        &State,
        ExecuteNullBoolean
    }
};

constexpr HaseEventRegistration EventRegistrations[] = {
    {
        &Instruments[0],
        &Events[0]
    }
};

const HaseEndpointDefinition Definition = {
    &Endpoint,
    PropertyRegistrations,
    2,
    CommandRegistrations,
    1,
    EventRegistrations,
    1
};

constexpr HasePropertyRegistration ResolverProperties[] = {
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

constexpr HaseCommandRegistration ResolverCommands[] = {
    {
        &Instruments[0],
        &Commands[0],
        nullptr,
        ExecuteNullBoolean
    }
};

constexpr HaseEndpointDefinition ResolverDefinition = {
    &Endpoint,
    ResolverProperties,
    2,
    ResolverCommands,
    1,
    EventRegistrations,
    1
};

static_assert(
    HaseEndpointDefinitionResolver::ResolveInstrument(
        ResolverDefinition,
        "instrument-01") == &Instruments[0],
    "A known instrument must resolve to its descriptor.");

static_assert(
    HaseEndpointDefinitionResolver::ResolveInstrument(
        ResolverDefinition,
        "missing") == nullptr,
    "An unknown instrument must not resolve.");

static_assert(
    HaseEndpointDefinitionResolver::ResolveProperty(
        ResolverDefinition,
        "instrument-01",
        "temperature") == &ResolverProperties[0],
    "A known Property must resolve to its registration.");

static_assert(
    HaseEndpointDefinitionResolver::ResolveProperty(
        ResolverDefinition,
        "missing",
        "temperature") == nullptr,
    "A Property on an unknown instrument must not resolve.");

static_assert(
    HaseEndpointDefinitionResolver::ResolveProperty(
        ResolverDefinition,
        "instrument-01",
        "missing") == nullptr,
    "An unknown Property must not resolve.");

static_assert(
    HaseEndpointDefinitionResolver::ResolveCommand(
        ResolverDefinition,
        "instrument-01",
        "Controller.Toggle") == &ResolverCommands[0],
    "A known Command must resolve to its registration.");

static_assert(
    HaseEndpointDefinitionResolver::ResolveCommand(
        ResolverDefinition,
        "instrument-01",
        "missing") == nullptr,
    "An unknown Command must not resolve.");

static_assert(
    HaseEndpointDefinitionResolver::ResolveEvent(
        ResolverDefinition,
        "instrument-01",
        "Controller.Pressed") == &EventRegistrations[0],
    "A known Event must resolve to its registration.");

static_assert(
    HaseEndpointDefinitionResolver::ResolveEvent(
        ResolverDefinition,
        "instrument-01",
        "missing") == nullptr,
    "An unknown Event must not resolve.");

void ProcessEnvelope(
    const HaseProtocolEnvelope& envelope,
    HaseProtocolDispatchResult dispatchResult)
{
    uint8_t responseFrame[4096];
    uint32_t responseFrameLength =
        0;

    HaseUtcClock utcClock;

    volatile HaseEndpointRequestProcessingResult result =
        HaseEndpointRequestProcessor::Process(
            envelope,
            dispatchResult,
            Definition,
            utcClock,
            responseFrame,
            sizeof(responseFrame),
            responseFrameLength);

    (void)result;
}

HaseProtocolEnvelope CreateEnvelope(
    uint8_t messageType,
    uint8_t* payload,
    uint32_t payloadLength)
{
    HaseProtocolEnvelope envelope;

    envelope.majorVersion =
        1;

    envelope.minorVersion =
        0;

    envelope.role =
        1;

    envelope.messageType =
        messageType;

    envelope.correlationId =
        1;

    envelope.payload =
        payload;

    envelope.payloadLength =
        payloadLength;

    return envelope;
}

void setup()
{
    uint8_t payload[256];

    HaseProtocolEnvelope emptyEnvelope =
        CreateEnvelope(0, nullptr, 0);

    ProcessEnvelope(
        emptyEnvelope,
        HaseProtocolDispatchResult::DiscoverRequestRecognized);

    ProcessEnvelope(
        emptyEnvelope,
        HaseProtocolDispatchResult::ReadEndpointDescriptorRequestRecognized);

    ProcessEnvelope(
        emptyEnvelope,
        HaseProtocolDispatchResult::InvalidDiscoverRequest);

    ProcessEnvelope(
        emptyEnvelope,
        HaseProtocolDispatchResult::InvalidReadPropertyRequest);

    ProcessEnvelope(
        emptyEnvelope,
        HaseProtocolDispatchResult::InvalidWritePropertyRequest);

    ProcessEnvelope(
        emptyEnvelope,
        HaseProtocolDispatchResult::InvalidExecuteCommandRequest);

    ProcessEnvelope(
        emptyEnvelope,
        HaseProtocolDispatchResult::InvalidReadEndpointDescriptorRequest);

    ProcessEnvelope(
        emptyEnvelope,
        HaseProtocolDispatchResult::UnsupportedVersion);

    ProcessEnvelope(
        emptyEnvelope,
        HaseProtocolDispatchResult::UnsupportedMessage);

    HaseBinaryProtocolWriter readWriter(
        payload,
        sizeof(payload));

    readWriter.writeString("instrument-01");
    readWriter.writeString("temperature");

    HaseProtocolEnvelope readEnvelope =
        CreateEnvelope(
            10,
            payload,
            static_cast<uint32_t>(readWriter.length()));

    ProcessEnvelope(
        readEnvelope,
        HaseProtocolDispatchResult::ReadPropertyRequestRecognized);

    HaseBinaryProtocolWriter booleanReadWriter(
        payload,
        sizeof(payload));

    booleanReadWriter.writeString("instrument-01");
    booleanReadWriter.writeString("enabled");

    HaseProtocolEnvelope booleanReadEnvelope =
        CreateEnvelope(
            10,
            payload,
            static_cast<uint32_t>(booleanReadWriter.length()));

    ProcessEnvelope(
        booleanReadEnvelope,
        HaseProtocolDispatchResult::ReadPropertyRequestRecognized);

    HaseBinaryProtocolWriter writeWriter(
        payload,
        sizeof(payload));

    writeWriter.writeString("instrument-01");
    writeWriter.writeString("enabled");
    writeWriter.writeByte(1);
    writeWriter.writeByte(1);

    HaseProtocolEnvelope writeEnvelope =
        CreateEnvelope(
            20,
            payload,
            static_cast<uint32_t>(writeWriter.length()));

    ProcessEnvelope(
        writeEnvelope,
        HaseProtocolDispatchResult::WritePropertyRequestRecognized);

    HaseBinaryProtocolWriter commandWriter(
        payload,
        sizeof(payload));

    commandWriter.writeString("instrument-01");
    commandWriter.writeString("Controller.Toggle");
    commandWriter.writeByte(0);

    HaseProtocolEnvelope commandEnvelope =
        CreateEnvelope(
            30,
            payload,
            static_cast<uint32_t>(commandWriter.length()));

    ProcessEnvelope(
        commandEnvelope,
        HaseProtocolDispatchResult::ExecuteCommandRequestRecognized);
}

void loop()
{
}
