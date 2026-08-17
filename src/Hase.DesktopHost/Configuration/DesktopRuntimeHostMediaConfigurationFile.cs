using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hase.Runtime.Media;

namespace Hase.DesktopHost.Configuration;

public static class DesktopRuntimeHostMediaConfigurationFile
{
    private const int CurrentFormatVersion = 1;
    private const int MaximumDocumentByteCount = 64 * 1024;
    private const int MaximumSources = 16;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    public static async Task<DesktopRuntimeHostMediaConfiguration> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException(
                "The Runtime Host media-configuration path must be fully qualified.",
                nameof(filePath));
        }

        await using FileStream stream = new(
            Path.GetFullPath(filePath), FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] document = new byte[MaximumDocumentByteCount + 1];
        int length = 0;
        while (length < document.Length)
        {
            int read = await stream.ReadAsync(
                document.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            length += read;
        }

        if (length > MaximumDocumentByteCount)
        {
            throw new InvalidDataException(
                "The Runtime Host media configuration exceeds the supported size.");
        }

        return Parse(document.AsSpan(0, length));
    }

    internal static DesktopRuntimeHostMediaConfiguration Parse(
        ReadOnlySpan<byte> document)
    {
        try
        {
            if (document.Length >= 3 && document[0] == 0xEF &&
                document[1] == 0xBB && document[2] == 0xBF)
            {
                document = document[3..];
            }

            MediaDocument? parsed = JsonSerializer.Deserialize<MediaDocument>(
                document, SerializerOptions);
            if (parsed is null || parsed.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException(
                    "The Runtime Host media-configuration format is not supported.");
            }
            if (parsed.Sources is null || parsed.Sources.Count is < 1 or > MaximumSources)
            {
                throw new InvalidDataException(
                    "The Runtime Host media configuration requires between one and sixteen sources.");
            }

            RuntimeHostMediaSourceConfiguration[] sources =
                parsed.Sources.Select(CreateSource).ToArray();
            if (sources.Select(item => item.Target.MediaSourceId)
                .Distinct(StringComparer.Ordinal).Count() != sources.Length)
            {
                throw new InvalidDataException(
                    "The Runtime Host media configuration contains a duplicate logical source identity.");
            }

            return new DesktopRuntimeHostMediaConfiguration(sources);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The Runtime Host media configuration is not valid JSON.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The Runtime Host media configuration contains invalid data.", exception);
        }
    }

    private static RuntimeHostMediaSourceConfiguration CreateSource(
        MediaSourceDocument source)
    {
        if (source is null)
        {
            throw new InvalidDataException(
                "The Runtime Host media configuration contains a null source.");
        }

        string? audioDeviceId = string.IsNullOrWhiteSpace(source.AudioDeviceId)
            ? null
            : source.AudioDeviceId.Trim();
        return new RuntimeHostMediaSourceConfiguration(
            new RuntimeHostMediaSourceTarget(
                Require(source.MediaSourceId, "logical source identity"),
                Require(source.MediaSourceGeneration, "source generation")),
            Require(source.VideoDeviceId, "video device identity"),
            audioDeviceId,
            RuntimeHostMediaSourceAvailability.Idle,
            Require(source.DisplayName, "source display name"));
    }

    private static string Require(string? value, string role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"The media {role} is required.");
        }
        return value.Trim();
    }

    private sealed class MediaDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; }

        [JsonPropertyName("sources")]
        public List<MediaSourceDocument>? Sources { get; set; }
    }

    private sealed class MediaSourceDocument
    {
        [JsonPropertyName("mediaSourceId")]
        public string? MediaSourceId { get; set; }
        [JsonPropertyName("mediaSourceGeneration")]
        public string? MediaSourceGeneration { get; set; }
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }
        [JsonPropertyName("videoDeviceId")]
        public string? VideoDeviceId { get; set; }
        [JsonPropertyName("audioDeviceId")]
        public string? AudioDeviceId { get; set; }
    }
}
