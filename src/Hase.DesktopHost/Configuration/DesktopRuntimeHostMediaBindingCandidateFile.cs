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
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(candidate);
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
            sources = new[]
            {
                new
                {
                    mediaSourceId = candidate.MediaSourceId,
                    mediaSourceGeneration = candidate.MediaSourceGeneration,
                    displayName = candidate.DisplayName,
                    videoDeviceId = candidate.VideoDeviceId,
                    audioDeviceId = candidate.AudioDeviceId
                }
            }
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
