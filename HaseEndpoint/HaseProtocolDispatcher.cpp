#include "HaseProtocolDispatcher.h"

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
                HaseProtocolDispatchResult::InvalidDiscoverRequest;
        }

        return
            HaseProtocolDispatchResult::DiscoverRequestRecognized;
    }

    return
        HaseProtocolDispatchResult::UnsupportedMessage;
}