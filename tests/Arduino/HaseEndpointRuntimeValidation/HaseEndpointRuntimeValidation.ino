#include <HaseEsp32Endpoint.h>

namespace
{
    class ValidationApplication final : public HaseEndpointApplication
    {
    public:
        bool beginHardware() override
        {
            beginHardwareCallCount++;

            return true;
        }

        void beginEventDetection() override
        {
            beginEventDetectionCallCount++;
        }

        void update(
            HaseEndpointRuntime&) override
        {
            updateCallCount++;
        }

        uint32_t beginHardwareCallCount =
            0;

        uint32_t beginEventDetectionCallCount =
            0;

        uint32_t updateCallCount =
            0;
    };

    const HaseEndpointMetadata EndpointMetadata =
    {
        "Runtime validation endpoint",
        "Compile-time validation for the public runtime facade."
    };

    const HaseEventDescriptor Events[] =
    {
        {
            "Validation.Event",
            "Validation Event",
            "Compile-time Event publication fixture."
        }
    };

    const HaseInstrumentDescriptor Instruments[] =
    {
        {
            "runtime-validation-instrument",
            "Runtime Validation Instrument",
            "validation",
            {},
            nullptr,
            0,
            nullptr,
            0,
            Events,
            1
        }
    };

    const HaseEndpointDescriptor Descriptor =
    {
        "runtime-validation-endpoint",
        EndpointMetadata,
        Instruments,
        1
    };

    const HaseEventRegistration EventRegistrations[] =
    {
        {
            &Instruments[0],
            &Events[0]
        }
    };

    const HaseEndpointDefinition Definition =
    {
        &Descriptor,
        nullptr,
        0,
        nullptr,
        0,
        EventRegistrations,
        1
    };

    constexpr HaseEndpointConfiguration Configuration =
    {
        5000,
        "runtime-validation-endpoint",
        "runtime-validation-endpoint",
        4096,
        5000,
        15000
    };

    ValidationApplication Application;

    HaseEndpointRuntime Runtime(
        Configuration,
        Definition,
        Application);

    volatile bool ExerciseNetworkOperations =
        false;
}

void setup()
{
    if (ExerciseNetworkOperations)
    {
        Runtime.begin(
            "VALIDATION_SSID",
            "VALIDATION_PASSWORD");

        Runtime.update();

        Runtime.publishNullEvent(
            EventRegistrations[0]);
    }

    const bool started =
        Runtime.isStarted();

    (void)started;
}

void loop()
{
}
