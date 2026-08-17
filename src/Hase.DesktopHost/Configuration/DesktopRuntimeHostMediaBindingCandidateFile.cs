using System.Text.Json;

namespace Hase.DesktopHost.Configuration;

public static class DesktopRuntimeHostMediaBindingCandidateFile
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static async Task WriteNewAsync(
        string filePath,
        DesktopRuntimeHostMediaBindingCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        await WriteNewAsync(
            filePath,
            new[] { candidate },
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task WriteNewAsync(
        string filePath,
        IReadOnlyList<DesktopRuntimeHostMediaBindingCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count is < 1 or > 16 || candidates.Any(item => item is null))
        {
            throw new ArgumentException(
                "A media binding candidate requires between one and sixteen sources.",
                nameof(candidates));
        }
        if (candidates.Select(item => item.VideoDeviceId)
            .Distinct(StringComparer.Ordinal).Count() != candidates.Count)
        {
            throw new ArgumentException(
                "Each camera may occur only once in a media binding candidate.",
                nameof(candidates));
        }
        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException(
                "The media binding candidate path must be fully qualified.",
                nameof(filePath));
        }

        string fullPath = Path.GetFullPath(filePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                "The media binding candidate directory does not exist.");
        }
        if (File.Exists(fullPath))
        {
            throw new IOException(
                "The media binding candidate already exists.");
        }

        var document = new
        {
            formatVersion = 1,
            sources = candidates.Select(candidate =>
                new
                {
                    mediaSourceId = candidate.MediaSourceId,
                    mediaSourceGeneration = candidate.MediaSourceGeneration,
                    displayName = candidate.DisplayName,
                    videoDeviceId = candidate.VideoDeviceId,
                    audioDeviceId = candidate.AudioDeviceId
                }).ToArray()
        };

        string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") +
            ".tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    document,
                    SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            _ = await DesktopRuntimeHostMediaConfigurationFile.LoadAsync(
                temporaryPath,
                cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, fullPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
