using System.Text;
using System.Text.Json;
using Hase.Client.Media;

namespace Hase.Client.Wpf.AppHost.Media;

public enum ClientMediaWebMessageKind
{
    Ready,
    PresentationStarted,
    PresentationStopped,
    PresentationFaulted,
    AudioActivationBlocked,
    Negotiation,
    PeerConnected
}

public sealed record ClientMediaWebMessage(
    ClientMediaWebMessageKind Kind,
    string? FailureCode,
    RemoteMediaNegotiationMessage? NegotiationMessage = null);

public sealed class ClientMediaWebMessageValidator
{
    public const int MaximumSessionDescriptionUtf8Bytes = 49_152;
    public const int MaximumIceCandidateUtf8Bytes = 4_096;
    public const int MaximumMessageUtf8Bytes =
        (MaximumSessionDescriptionUtf8Bytes * 2) + 1_024;
    private static readonly HashSet<string> FailureCodes =
        new(StringComparer.Ordinal)
        {
            "transport-unavailable",
            "decode-failed",
            "playback-blocked",
            "browser-failed",
            "negotiation-rejected",
            "codec-unsupported"
        };

    public bool TryValidate(string? json, out ClientMediaWebMessage? message)
    {
        message = null;
        if (string.IsNullOrWhiteSpace(json) ||
            Encoding.UTF8.GetByteCount(json) > MaximumMessageUtf8Bytes)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json,
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
                "ready" => ClientMediaWebMessageKind.Ready,
                "presentation-started" =>
                    ClientMediaWebMessageKind.PresentationStarted,
                "presentation-stopped" =>
                    ClientMediaWebMessageKind.PresentationStopped,
                "presentation-faulted" =>
                    ClientMediaWebMessageKind.PresentationFaulted,
                "audio-activation-blocked" =>
                    ClientMediaWebMessageKind.AudioActivationBlocked,
                "negotiation" => ClientMediaWebMessageKind.Negotiation,
                "peer-connected" => ClientMediaWebMessageKind.PeerConnected,
                _ => (ClientMediaWebMessageKind?)null
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

            var presentationFaulted =
                kind == ClientMediaWebMessageKind.PresentationFaulted;
            var audioActivationBlocked =
                kind == ClientMediaWebMessageKind.AudioActivationBlocked;
            var failureIsValid = failureCode is not null &&
                FailureCodes.Contains(failureCode);
            if ((presentationFaulted && !failureIsValid) ||
                (audioActivationBlocked &&
                    failureCode != "playback-blocked") ||
                (!presentationFaulted && !audioActivationBlocked &&
                    failureCode is not null))
            {
                return false;
            }

            RemoteMediaNegotiationMessage? negotiationMessage = null;
            if (kind == ClientMediaWebMessageKind.Negotiation)
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

    private static RemoteMediaNegotiationMessage? ParseNegotiation(
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
            "answer" => RemoteMediaNegotiationKind.Answer,
            "ice-candidate" => RemoteMediaNegotiationKind.IceCandidate,
            "ice-complete" => RemoteMediaNegotiationKind.IceComplete,
            _ => (RemoteMediaNegotiationKind?)null
        };
        if (kind is null)
        {
            return null;
        }

        var payload = payloadElement.GetString() ?? "";
        var payloadBytes = Encoding.UTF8.GetByteCount(payload);
        var valid = kind switch
        {
            RemoteMediaNegotiationKind.Answer =>
                payloadBytes is > 0 and <= MaximumSessionDescriptionUtf8Bytes,
            RemoteMediaNegotiationKind.IceCandidate =>
                payloadBytes is > 0 and <= MaximumIceCandidateUtf8Bytes,
            RemoteMediaNegotiationKind.IceComplete => payloadBytes == 0,
            _ => false
        };
        return valid
            ? new RemoteMediaNegotiationMessage(sequence, kind.Value, payload)
            : null;
    }
}
