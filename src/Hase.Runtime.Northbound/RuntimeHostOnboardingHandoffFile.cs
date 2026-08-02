using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Strictly reads and atomically creates one non-secret Runtime Host identity
/// handoff document.
/// </summary>
public static class RuntimeHostOnboardingHandoffFile
{
    private const int CurrentFormatVersion = 1;
    private const int MaximumDocumentByteCount = 4096;
    private static readonly JsonSerializerOptions ReaderOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 4
    };

    public static async Task<RuntimeHostOnboardingHandoff> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        string path = NormalizePath(filePath);
        await using FileStream stream = new(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] document = new byte[MaximumDocumentByteCount + 1];
        int length = 0;
        while (length < document.Length)
        {
            int read = await stream.ReadAsync(document.AsMemory(length), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) break;
            length += read;
        }

        if (length > MaximumDocumentByteCount)
            throw new InvalidDataException("The Runtime Host onboarding handoff exceeds the supported size.");

        try
        {
            HandoffDocument? parsed = JsonSerializer.Deserialize<HandoffDocument>(
                document.AsSpan(0, length), ReaderOptions);
            if (parsed is null || parsed.FormatVersion != CurrentFormatVersion)
                throw new InvalidDataException("The Runtime Host onboarding handoff format version is not supported.");
            return new RuntimeHostOnboardingHandoff(
                new RuntimeHostId(parsed.RuntimeHostId
                    ?? throw new InvalidDataException("The authoritative Runtime Host identity is required.")));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Runtime Host onboarding handoff is not valid JSON configuration.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The Runtime Host onboarding handoff contains invalid configuration.", exception);
        }
    }

    public static async Task CreateAsync(
        string filePath,
        RuntimeHostId runtimeHostId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeHostId);
        string path = NormalizePath(filePath);
        if (File.Exists(path))
            throw new IOException("The Runtime Host onboarding handoff destination already exists.");

        string directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("The handoff path has no parent directory.", nameof(filePath));
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException("The Runtime Host onboarding handoff destination directory does not exist.");

        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream stream = new(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, new HandoffDocument
                {
                    FormatVersion = CurrentFormatVersion,
                    RuntimeHostId = runtimeHostId.Value
                }, new JsonSerializerOptions { WriteIndented = true }, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            RuntimeHostOnboardingHandoff candidate = await LoadAsync(temporaryPath, cancellationToken)
                .ConfigureAwait(false);
            if (candidate.RuntimeHostId != runtimeHostId)
                throw new InvalidDataException("The Runtime Host onboarding handoff candidate identity changed.");
            File.Move(temporaryPath, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static string NormalizePath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!Path.IsPathFullyQualified(filePath))
            throw new ArgumentException("The Runtime Host onboarding handoff path must be fully qualified.", nameof(filePath));
        return Path.GetFullPath(filePath);
    }

    private sealed class HandoffDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; }
        [JsonPropertyName("runtimeHostId")]
        public string? RuntimeHostId { get; set; }
    }
}
