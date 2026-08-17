using System.Text;
using System.Text.Json;

namespace Hase.DesktopHost.App.Media;

public sealed class RuntimeHostMediaBindingWebMessageValidator
{
    public const int MaximumMessageUtf8Bytes = 16 * 1024;
    public const int MaximumDeviceIdCharacters = 4096;

    private static readonly HashSet<string> FailureCodes =
        new(StringComparer.Ordinal)
        {
            "device-unavailable",
            "permission-denied",
            "enumeration-failed",
            "browser-failed"
        };

    public bool TryValidate(
        string? json,
        out RuntimeHostMediaBindingWebMessage? message)
    {
        message = null;
        if (string.IsNullOrWhiteSpace(json) ||
            Encoding.UTF8.GetByteCount(json) > MaximumMessageUtf8Bytes)
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 3
                });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !HasOnlyKnownProperties(root) ||
                !root.TryGetProperty("version", out JsonElement version) ||
                version.ValueKind != JsonValueKind.Number ||
                !version.TryGetInt32(out int versionValue) ||
                versionValue != 1 ||
                !root.TryGetProperty("kind", out JsonElement kindElement) ||
                kindElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            RuntimeHostMediaBindingWebMessageKind? kind =
                kindElement.GetString() switch
                {
                    "ready" => RuntimeHostMediaBindingWebMessageKind.Ready,
                    "discovery-requested" =>
                        RuntimeHostMediaBindingWebMessageKind.DiscoveryRequested,
                    "selection-confirmed" =>
                        RuntimeHostMediaBindingWebMessageKind.SelectionConfirmed,
                    "cancelled" =>
                        RuntimeHostMediaBindingWebMessageKind.Cancelled,
                    "faulted" => RuntimeHostMediaBindingWebMessageKind.Faulted,
                    _ => null
                };
            if (kind is null)
            {
                return false;
            }

            string? videoDeviceId = ReadOptionalString(root, "videoDeviceId");
            string? audioDeviceId = ReadOptionalString(root, "audioDeviceId");
            string? failureCode = ReadOptionalString(root, "failureCode");
            if (videoDeviceId is { Length: > MaximumDeviceIdCharacters } ||
                audioDeviceId is { Length: > MaximumDeviceIdCharacters })
            {
                return false;
            }

            bool selection =
                kind == RuntimeHostMediaBindingWebMessageKind.SelectionConfirmed;
            if (selection != !string.IsNullOrWhiteSpace(videoDeviceId))
            {
                return false;
            }
            if (!selection && audioDeviceId is not null)
            {
                return false;
            }

            bool failure = kind == RuntimeHostMediaBindingWebMessageKind.Faulted;
            if (failure !=
                (failureCode is not null && FailureCodes.Contains(failureCode)))
            {
                return false;
            }

            message = new RuntimeHostMediaBindingWebMessage(
                kind.Value,
                videoDeviceId,
                string.IsNullOrWhiteSpace(audioDeviceId)
                    ? null
                    : audioDeviceId,
                failureCode);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadOptionalString(
        JsonElement root,
        string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Null => null,
            _ => throw new JsonException($"{name} must be a string or null.")
        };
    }

    private static bool HasOnlyKnownProperties(JsonElement root)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name is not (
                "version" or
                "kind" or
                "videoDeviceId" or
                "audioDeviceId" or
                "failureCode"))
            {
                return false;
            }
        }
        return true;
    }
}
