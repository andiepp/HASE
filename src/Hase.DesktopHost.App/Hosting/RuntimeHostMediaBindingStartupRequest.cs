using Hase.DesktopHost.Configuration;
using System.IO;

namespace Hase.DesktopHost.App.Hosting;

public sealed record RuntimeHostMediaBindingStartupRequest
{
    public const string Command = "--prepare-media-binding";

    private RuntimeHostMediaBindingStartupRequest(
        string outputFilePath,
        string mediaSourceId,
        string mediaSourceGeneration,
        string displayName)
    {
        OutputFilePath = outputFilePath;
        MediaSourceId = mediaSourceId;
        MediaSourceGeneration = mediaSourceGeneration;
        DisplayName = displayName;
    }

    public string OutputFilePath { get; }
    public string MediaSourceId { get; }
    public string MediaSourceGeneration { get; }
    public string DisplayName { get; }

    public static RuntimeHostMediaBindingStartupRequest? Parse(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 0 ||
            !string.Equals(arguments[0], Command, StringComparison.Ordinal))
        {
            return null;
        }
        if (arguments.Count != 5)
        {
            throw new ArgumentException(
                "Media binding mode requires an output path, source identity, source generation, and display name.",
                nameof(arguments));
        }

        string outputPath = arguments[1];
        if (string.IsNullOrWhiteSpace(outputPath) ||
            !Path.IsPathFullyQualified(outputPath))
        {
            throw new ArgumentException(
                "The media binding candidate output path must be fully qualified.",
                nameof(arguments));
        }

        string fullOutputPath = Path.GetFullPath(outputPath);
        _ = new DesktopRuntimeHostMediaBindingCandidate(
            arguments[2],
            arguments[3],
            arguments[4],
            "validation-device",
            null);
        return new RuntimeHostMediaBindingStartupRequest(
            fullOutputPath,
            arguments[2].Trim(),
            arguments[3].Trim(),
            arguments[4].Trim());
    }

    public DesktopRuntimeHostMediaBindingCandidate CreateCandidate(
        string videoDeviceId,
        string? audioDeviceId) =>
        new(
            MediaSourceId,
            MediaSourceGeneration,
            DisplayName,
            videoDeviceId,
            audioDeviceId);

    public override string ToString() =>
        "Runtime Host local media binding request";
}
