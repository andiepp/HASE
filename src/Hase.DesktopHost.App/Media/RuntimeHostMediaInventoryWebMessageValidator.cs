using System.Text;
using System.Text.Json;
using Hase.Runtime.Media;

namespace Hase.DesktopHost.App.Media;

public sealed class RuntimeHostMediaInventoryWebMessageValidator
{
    public const int MaximumDeviceIdentityUtf8Bytes = 512;
    public const int MaximumMessageUtf8Bytes = 12 * 1024;

    public bool TryValidate(
        string? json,
        out IReadOnlyList<RuntimeHostMediaDeviceObservation>? observations)
    {
        observations = null;
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
                    MaxDepth = 4
                });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().Any(property =>
                    property.Name is not ("version" or "kind" or "devices")) ||
                !root.TryGetProperty("version", out var version) ||
                !version.TryGetInt32(out int versionValue) ||
                versionValue != 1 ||
                !root.TryGetProperty("kind", out var kind) ||
                kind.ValueKind != JsonValueKind.String ||
                kind.GetString() != "inventory" ||
                !root.TryGetProperty("devices", out var devices) ||
                devices.ValueKind != JsonValueKind.Array ||
                devices.GetArrayLength() >
                    RuntimeHostMediaInventoryReconciler.MaximumSources)
            {
                return false;
            }

            var result = new List<RuntimeHostMediaDeviceObservation>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement item in devices.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    item.EnumerateObject().Any(property =>
                        property.Name != "deviceId") ||
                    !item.TryGetProperty("deviceId", out var identity) ||
                    identity.ValueKind != JsonValueKind.String)
                {
                    return false;
                }
                string? value = identity.GetString();
                if (string.IsNullOrWhiteSpace(value) ||
                    Encoding.UTF8.GetByteCount(value) >
                        MaximumDeviceIdentityUtf8Bytes ||
                    !identities.Add(value))
                {
                    return false;
                }
                result.Add(new(value));
            }

            observations = result;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
