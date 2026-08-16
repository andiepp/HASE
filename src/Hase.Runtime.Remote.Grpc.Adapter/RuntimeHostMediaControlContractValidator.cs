using System.Text;
using MediaV1 = global::Hase.Runtime.Media.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Rejects malformed or oversized version 1 media-control values before they
/// can reach session or browser-media state.
/// </summary>
public sealed class RuntimeHostMediaControlContractValidator
{
    public void ValidateSourceTarget(
        MediaV1.MediaSourceTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        ValidateRequiredUtf8(
            target.MediaSourceId,
            RuntimeHostMediaControlLimits.MaximumSourceIdentityUtf8Bytes,
            "media_source_id");
        ValidateRequiredUtf8(
            target.MediaSourceGeneration,
            RuntimeHostMediaControlLimits.MaximumSourceIdentityUtf8Bytes,
            "media_source_generation");
    }

    public void ValidateSessionId(
        string sessionId)
    {
        ValidateRequiredUtf8(
            sessionId,
            RuntimeHostMediaControlLimits.MaximumSessionIdUtf8Bytes,
            "session_id");
    }

    public void ValidateNegotiationMessage(
        MediaV1.MediaNegotiationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Sequence == 0)
        {
            throw new ArgumentException(
                "The negotiation sequence must be greater than zero.",
                nameof(message));
        }

        switch (message.Kind)
        {
            case MediaV1.MediaNegotiationMessageKind.Offer:
            case MediaV1.MediaNegotiationMessageKind.Answer:
                ValidateRequiredUtf8(
                    message.SensitivePayload,
                    RuntimeHostMediaControlLimits
                        .MaximumSessionDescriptionUtf8Bytes,
                    "sensitive_payload");
                break;

            case MediaV1.MediaNegotiationMessageKind.IceCandidate:
                ValidateRequiredUtf8(
                    message.SensitivePayload,
                    RuntimeHostMediaControlLimits
                        .MaximumIceCandidateUtf8Bytes,
                    "sensitive_payload");
                break;

            case MediaV1.MediaNegotiationMessageKind.IceComplete:
                if (message.SensitivePayload.Length != 0)
                {
                    throw new ArgumentException(
                        "ICE completion cannot contain a payload.",
                        nameof(message));
                }

                break;

            case MediaV1.MediaNegotiationMessageKind.Unspecified:
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(message),
                    message.Kind,
                    "A supported negotiation message kind is required.");
        }
    }

    private static void ValidateRequiredUtf8(
        string value,
        int maximumByteCount,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{fieldName} must have a non-empty value.",
                fieldName);
        }

        if (Encoding.UTF8.GetByteCount(value) > maximumByteCount)
        {
            throw new ArgumentException(
                $"{fieldName} exceeds its UTF-8 byte limit.",
                fieldName);
        }
    }
}
