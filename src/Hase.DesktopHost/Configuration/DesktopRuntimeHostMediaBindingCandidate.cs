using System.Text;
using Hase.Runtime.Media;

namespace Hase.DesktopHost.Configuration;

public sealed record DesktopRuntimeHostMediaBindingCandidate
{
    public DesktopRuntimeHostMediaBindingCandidate(
        string mediaSourceId,
        string mediaSourceGeneration,
        string displayName,
        string videoDeviceId,
        string? audioDeviceId)
    {
        MediaSourceId = RequireIdentity(mediaSourceId, nameof(mediaSourceId));
        MediaSourceGeneration = RequireIdentity(
            mediaSourceGeneration,
            nameof(mediaSourceGeneration));
        DisplayName = RequireIdentity(displayName, nameof(displayName));
        VideoDeviceId = RequireDeviceId(videoDeviceId, nameof(videoDeviceId));
        AudioDeviceId = string.IsNullOrWhiteSpace(audioDeviceId)
            ? null
            : RequireDeviceId(audioDeviceId, nameof(audioDeviceId));
    }

    public string MediaSourceId { get; }
    public string MediaSourceGeneration { get; }
    public string DisplayName { get; }
    public string VideoDeviceId { get; }
    public string? AudioDeviceId { get; }

    public override string ToString() =>
        $"Runtime Host media binding candidate ({MediaSourceId}, audio: {AudioDeviceId is not null})";

    private static string Require(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string RequireIdentity(string value, string parameterName)
    {
        string result = Require(value, parameterName);
        if (Encoding.UTF8.GetByteCount(result) >
            RuntimeHostMediaSessionOwner.MaximumIdentityUtf8Bytes)
        {
            throw new ArgumentException(
                "The media identity exceeds the supported UTF-8 size.",
                parameterName);
        }
        return result;
    }

    private static string RequireDeviceId(string value, string parameterName)
    {
        string result = Require(value, parameterName);
        if (result.Length > 4096)
        {
            throw new ArgumentException(
                "The media device identity exceeds the supported size.",
                parameterName);
        }
        return result;
    }
}
