/* Protocol Dispatcher */

#include "HaseProtocolDispatcher.h"
#include "HaseBinaryProtocolReader.h"

namespace
{
    bool IsValidReadEndpointDescriptorRequestPayload(
        const HaseProtocolEnvelope& envelope)
    {
        HaseBinaryProtocolReader reader(
            envelope.payload,
            envelope.payloadLength);

        uint16_t endpointIdLength =
            0;

        if (!reader.readUInt16(
                endpointIdLength))
        {
            return false;
        }

        if (endpointIdLength == 0)
        {
            return false;
        }

        if (!reader.skipBytes(
                endpointIdLength))
        {
            return false;
        }

        return reader.fullyConsumed();
    }
}

HaseProtocolDispatchResult HaseProtocolDispatcher::Dispatch(
    const HaseProtocolEnvelope& envelope)
{
    if (envelope.majorVersion
            != SupportedMajorVersion
        || envelope.minorVersion
            != SupportedMinorVersion)
    {
        return
            HaseProtocolDispatchResult::UnsupportedVersion;
    }

    if (envelope.messageType
        == DiscoverRequestMessageType)
    {
        if (envelope.role
                != RequestRole
            || envelope.payloadLength
                != 0)
        {
            return
                HaseProtocolDispatchResult::
                    InvalidDiscoverRequest;
        }

        return
            HaseProtocolDispatchResult::
                DiscoverRequestRecognized;
    }

    if (envelope.messageType
        == ReadEndpointDescriptorRequestMessageType)
    {
        if (envelope.role
                != RequestRole
            || !IsValidReadEndpointDescriptorRequestPayload(
                envelope))
        {
            return
                HaseProtocolDispatchResult::
                    InvalidReadEndpointDescriptorRequest;
        }

        return
            HaseProtocolDispatchResult::
                ReadEndpointDescriptorRequestRecognized;
    }

    return
        HaseProtocolDispatchResult::UnsupportedMessage;
}