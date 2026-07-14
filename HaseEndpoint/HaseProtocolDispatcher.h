/* Protocol Dispatcher */

#pragma once

#include <Arduino.h>

#include "HaseProtocolEnvelope.h"

enum class HaseProtocolDispatchResult : uint8_t
{
    DiscoverRequestRecognized,

    ReadEndpointDescriptorRequestRecognized,

    UnsupportedVersion,

    InvalidDiscoverRequest,

    InvalidReadEndpointDescriptorRequest,

    UnsupportedMessage
};

class HaseProtocolDispatcher
{
public:
    static HaseProtocolDispatchResult Dispatch(
        const HaseProtocolEnvelope& envelope);

private:
    static constexpr uint8_t SupportedMajorVersion =
        1;

    static constexpr uint8_t SupportedMinorVersion =
        0;

    static constexpr uint8_t RequestRole =
        1;

    static constexpr uint8_t DiscoverRequestMessageType =
        1;

    static constexpr uint8_t ReadEndpointDescriptorRequestMessageType =
        52;
};