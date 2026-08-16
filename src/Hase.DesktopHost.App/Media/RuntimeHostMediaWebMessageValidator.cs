using System.Text;
using System.Text.Json;
using Hase.Runtime.Media;

namespace Hase.DesktopHost.App.Media;

/// <summary>
/// Accepts only a small versioned event envelope. It never accepts script,
/// URLs, device identities, signaling payloads, or arbitrary failure text.
/// </summary>
public sealed class RuntimeHostMediaWebMessageValidator
{
    public const int MaximumMessageUtf8Bytes =
        (RuntimeHostMediaSessionOwner.MaximumSessionDescriptionUtf8Bytes * 2) +
        1_024;

    private static readonly HashSet<string> FailureCodes =
        new(StringComparer.Ordinal)
        {
            "device-unavailable",
            "device-busy",
            "permission-denied",
            "constraint-rejected",
            "browser-failed",
            "negotiation-rejected",
            "codec-unsupported",
            "transport-failed"
        };

    public bool TryValidate(
        string? json,
        out RuntimeHostMediaWebMessage? message)
    {
        message = null;
        if (string.IsNullOrWhiteSpace(json) ||
            Encoding.UTF8.GetByteCount(json) > MaximumMessageUtf8Bytes)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 4
                });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !HasOnlyKnownProperties(root) ||
                !root.TryGetProperty("version", out var version) ||
                version.ValueKind != JsonValueKind.Number ||
                !version.TryGetInt32(out var versionValue) ||
                versionValue != 1 ||
                !root.TryGetProperty("kind", out var kindElement) ||
                kindElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var kind = kindElement.GetString() switch
            {
                "ready" => RuntimeHostMediaWebMessageKind.Ready,
                "capture-started" =>
                    RuntimeHostMediaWebMessageKind.CaptureStarted,
                "capture-stopped" =>
                    RuntimeHostMediaWebMessageKind.CaptureStopped,
                "capture-faulted" =>
                    RuntimeHostMediaWebMessageKind.CaptureFaulted,
                "negotiation" => RuntimeHostMediaWebMessageKind.Negotiation,
                "peer-connected" =>
                    RuntimeHostMediaWebMessageKind.PeerConnected,
                "peer-faulted" => RuntimeHostMediaWebMessageKind.PeerFaulted,
                _ => (RuntimeHostMediaWebMessageKind?)null
            };
            if (kind is null)
            {
                return false;
            }

            string? failureCode = null;
            if (root.TryGetProperty("failureCode", out var failureElement))
            {
                if (failureElement.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                failureCode = failureElement.GetString();
            }

            var isFailure = kind is
                RuntimeHostMediaWebMessageKind.CaptureFaulted or
                RuntimeHostMediaWebMessageKind.PeerFaulted;
            if (isFailure !=
                (failureCode is not null && FailureCodes.Contains(failureCode)))
            {
                return false;
            }

            RuntimeHostMediaNegotiationMessage? negotiationMessage = null;
            if (kind == RuntimeHostMediaWebMessageKind.Negotiation)
            {
                negotiationMessage = ParseNegotiation(root);
                if (negotiationMessage is null)
                {
                    return false;
                }
            }
            else if (HasNegotiationProperties(root))
            {
                return false;
            }

            message = new(kind.Value, failureCode, negotiationMessage);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasOnlyKnownProperties(JsonElement root)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name is not (
                "version" or
                "kind" or
                "failureCode" or
                "sequence" or
                "negotiationKind" or
                "sensitivePayload"))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasNegotiationProperties(JsonElement root) =>
        root.TryGetProperty("sequence", out _) ||
        root.TryGetProperty("negotiationKind", out _) ||
        root.TryGetProperty("sensitivePayload", out _);

    private static RuntimeHostMediaNegotiationMessage? ParseNegotiation(
        JsonElement root)
    {
        if (!root.TryGetProperty("sequence", out var sequenceElement) ||
            sequenceElement.ValueKind != JsonValueKind.Number ||
            !sequenceElement.TryGetUInt32(out var sequence) ||
            sequence == 0 ||
            !root.TryGetProperty("negotiationKind", out var kindElement) ||
            kindElement.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("sensitivePayload", out var payloadElement) ||
            payloadElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var kind = kindElement.GetString() switch
        {
            "offer" => RuntimeHostMediaNegotiationKind.Offer,
            "ice-candidate" => RuntimeHostMediaNegotiationKind.IceCandidate,
            "ice-complete" => RuntimeHostMediaNegotiationKind.IceComplete,
            _ => (RuntimeHostMediaNegotiationKind?)null
        };
        if (kind is null)
        {
            return null;
        }

        var payload = payloadElement.GetString() ?? "";
        var payloadBytes = Encoding.UTF8.GetByteCount(payload);
        var valid = kind switch
        {
            RuntimeHostMediaNegotiationKind.Offer =>
                payloadBytes is > 0 and <=
                    RuntimeHostMediaSessionOwner
                        .MaximumSessionDescriptionUtf8Bytes,
            RuntimeHostMediaNegotiationKind.IceCandidate =>
                payloadBytes is > 0 and <=
                    RuntimeHostMediaSessionOwner.MaximumIceCandidateUtf8Bytes,
            RuntimeHostMediaNegotiationKind.IceComplete => payloadBytes == 0,
            _ => false
        };
        return valid
            ? new RuntimeHostMediaNegotiationMessage(sequence, kind.Value, payload)
            : null;
    }
}
