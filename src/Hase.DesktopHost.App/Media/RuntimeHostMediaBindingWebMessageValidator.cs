using System.Text;
using System.Text.Json;

namespace Hase.DesktopHost.App.Media;

public sealed class RuntimeHostMediaBindingWebMessageValidator
{
    public const int MaximumMessageUtf8Bytes = 64 * 1024;
    public const int MaximumDeviceIdCharacters = 4096;
    public const int MaximumSelections = 16;

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

            string? failureCode = ReadOptionalString(root, "failureCode");
            bool selection =
                kind == RuntimeHostMediaBindingWebMessageKind.SelectionConfirmed;
            IReadOnlyList<RuntimeHostMediaBindingSelection>? selections =
                ReadSelections(root, selection);
            if (selection != (selections is not null))
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
                selections,
                failureCode);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyList<RuntimeHostMediaBindingSelection>? ReadSelections(
        JsonElement root,
        bool required)
    {
        if (!root.TryGetProperty("selections", out JsonElement value))
        {
            return null;
        }
        if (!required || value.ValueKind != JsonValueKind.Array ||
            value.GetArrayLength() is < 1 or > MaximumSelections)
        {
            throw new JsonException("selections must be a bounded array.");
        }

        var selections = new List<RuntimeHostMediaBindingSelection>();
        var videoIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !HasOnlySelectionProperties(item))
            {
                throw new JsonException("A selection has an invalid structure.");
            }
            string? videoDeviceId = ReadOptionalString(item, "videoDeviceId");
            string? audioDeviceId = ReadOptionalString(item, "audioDeviceId");
            if (string.IsNullOrWhiteSpace(videoDeviceId) ||
                videoDeviceId.Length > MaximumDeviceIdCharacters ||
                audioDeviceId is { Length: > MaximumDeviceIdCharacters } ||
                !videoIds.Add(videoDeviceId))
            {
                throw new JsonException("A selection contains invalid device identities.");
            }
            selections.Add(new RuntimeHostMediaBindingSelection(
                videoDeviceId,
                string.IsNullOrWhiteSpace(audioDeviceId) ? null : audioDeviceId));
        }
        return selections;
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
                "selections" or
                "failureCode"))
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasOnlySelectionProperties(JsonElement root)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name is not ("videoDeviceId" or "audioDeviceId"))
            {
                return false;
            }
        }
        return true;
    }
}
