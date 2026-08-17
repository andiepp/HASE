using Hase.DesktopHost.Configuration;
using Hase.DesktopHost.App.Media;
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

    public IReadOnlyList<DesktopRuntimeHostMediaBindingCandidate> CreateCandidates(
        IReadOnlyList<RuntimeHostMediaBindingSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        if (selections.Count is < 1 or > 16)
        {
            throw new ArgumentException(
                "Between one and sixteen cameras must be selected.",
                nameof(selections));
        }

        var candidates = new DesktopRuntimeHostMediaBindingCandidate[selections.Count];
        for (int index = 0; index < selections.Count; index++)
        {
            candidates[index] = new DesktopRuntimeHostMediaBindingCandidate(
                CreateSourceId(index),
                index == 0
                    ? MediaSourceGeneration
                    : Guid.NewGuid().ToString("N"),
                selections.Count == 1
                    ? DisplayName
                    : $"{DisplayName} {index + 1}",
                selections[index].VideoDeviceId,
                selections[index].AudioDeviceId);
        }
        return candidates;
    }

    private string CreateSourceId(int index)
    {
        if (index == 0)
        {
            return MediaSourceId;
        }

        int suffixStart = MediaSourceId.Length - 2;
        if (suffixStart > 0 &&
            MediaSourceId[suffixStart - 1] == '-' &&
            int.TryParse(MediaSourceId.AsSpan(suffixStart), out int suffix))
        {
            return $"{MediaSourceId[..suffixStart]}{suffix + index:D2}";
        }
        return $"{MediaSourceId}-{index + 1:D2}";
    }

    public override string ToString() =>
        "Runtime Host local media binding request";
}
