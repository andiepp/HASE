using System.Text;
using System.Text.Json;

namespace Hase.Client.Wpf.AppHost.Media;

public enum ClientMediaWebMessageKind
{
    Ready,
    PresentationStarted,
    PresentationStopped,
    PresentationFaulted
}

public sealed record ClientMediaWebMessage(
    ClientMediaWebMessageKind Kind,
    string? FailureCode);

public sealed class ClientMediaWebMessageValidator
{
    public const int MaximumMessageUtf8Bytes = 2_048;
    private static readonly HashSet<string> FailureCodes =
        new(StringComparer.Ordinal)
        {
            "transport-unavailable",
            "decode-failed",
            "playback-blocked",
            "browser-failed"
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

            if ((kind == ClientMediaWebMessageKind.PresentationFaulted) !=
                (failureCode is not null && FailureCodes.Contains(failureCode)))
            {
                return false;
            }

            message = new(kind.Value, failureCode);
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
            if (property.Name is not ("version" or "kind" or "failureCode"))
            {
                return false;
            }
        }
        return true;
    }
}
