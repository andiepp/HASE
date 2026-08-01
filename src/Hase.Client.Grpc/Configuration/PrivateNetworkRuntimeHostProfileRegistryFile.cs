using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hase.Client;
using Hase.Client.Configuration;

namespace Hase.Client.Grpc.Configuration;

/// <summary>
/// Loads one bounded, versioned client registry of expected private-network
/// Runtime Hosts.
/// </summary>
public static class PrivateNetworkRuntimeHostProfileRegistryFile
{
    private const int CurrentFormatVersion =
        1;

    private const int MaximumDocumentByteCount =
        128 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            PropertyNameCaseInsensitive =
                false,
            UnmappedMemberHandling =
                JsonUnmappedMemberHandling.Disallow,
            MaxDepth =
                8
        };

    public static async Task<PrivateNetworkRuntimeHostProfileRegistry> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            filePath);

        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            throw new ArgumentException(
                "The client runtime-host registry path must not be empty or whitespace.",
                nameof(filePath));
        }

        if (!Path.IsPathFullyQualified(
                filePath))
        {
            throw new ArgumentException(
                "The client runtime-host registry path must be fully qualified.",
                nameof(filePath));
        }

        cancellationToken.ThrowIfCancellationRequested();

        byte[] document =
            await ReadBoundedDocumentAsync(
                    Path.GetFullPath(
                        filePath),
                    cancellationToken)
                .ConfigureAwait(
                    false);

        return Parse(
            document);
    }

    private static async Task<byte[]> ReadBoundedDocumentAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using FileStream stream =
            new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous
                | FileOptions.SequentialScan);

        byte[] document =
            new byte[
                MaximumDocumentByteCount
                + 1];
        int documentLength =
            0;

        while (documentLength < document.Length)
        {
            int readLength =
                await stream.ReadAsync(
                        document.AsMemory(
                            documentLength),
                        cancellationToken)
                    .ConfigureAwait(
                        false);

            if (readLength == 0)
            {
                break;
            }

            documentLength +=
                readLength;
        }

        if (documentLength > MaximumDocumentByteCount)
        {
            throw new InvalidDataException(
                "The client runtime-host registry exceeds the supported size.");
        }

        return document[
            ..documentLength];
    }

    private static PrivateNetworkRuntimeHostProfileRegistry Parse(
        ReadOnlySpan<byte> document)
    {
        try
        {
            ReadOnlySpan<byte> jsonDocument =
                RemoveUtf8ByteOrderMark(
                    document);
            RegistryDocument? parsedDocument =
                JsonSerializer.Deserialize<RegistryDocument>(
                    jsonDocument,
                    SerializerOptions);

            if (parsedDocument is null)
            {
                throw new InvalidDataException(
                    "The client runtime-host registry must contain a JSON object.");
            }

            if (parsedDocument.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException(
                    "The client runtime-host registry format version is not supported.");
            }

            if (parsedDocument.Hosts is null)
            {
                throw new InvalidDataException(
                    "The client runtime-host registry hosts collection is required.");
            }

            return new PrivateNetworkRuntimeHostProfileRegistry(
                parsedDocument.Hosts.Select(
                    CreateProfile));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The client runtime-host registry is not valid JSON configuration.",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The client runtime-host registry contains invalid configuration.",
                exception);
        }
    }

    private static PrivateNetworkRuntimeHostProfile CreateProfile(
        HostDocument document)
    {
        if (document is null)
        {
            throw new InvalidDataException(
                "The client runtime-host registry must not contain a null host.");
        }

        if (document.Enabled is null)
        {
            throw new InvalidDataException(
                "Each client runtime-host profile requires an explicit enabled state.");
        }

        var profile =
            new RuntimeHostProfile(
                new RuntimeHostProfileId(
                    document.ProfileId
                    ?? throw new InvalidDataException(
                        "The client-local profile identity is required.")),
                document.DisplayName
                ?? throw new InvalidDataException(
                    "The runtime-host profile display name is required."),
                new RemoteRuntimeHostId(
                    document.ExpectedRuntimeHostId
                    ?? throw new InvalidDataException(
                        "The expected runtime-host identity is required.")),
                document.Enabled.Value);

        return new PrivateNetworkRuntimeHostProfile(
            profile,
            document.PrivateNetworkConfigurationFilePath
            ?? throw new InvalidDataException(
                "The private-network client configuration file path is required."));
    }

    private static ReadOnlySpan<byte> RemoveUtf8ByteOrderMark(
        ReadOnlySpan<byte> document) =>
        document.Length >= 3
        && document[0] == 0xEF
        && document[1] == 0xBB
        && document[2] == 0xBF
            ? document[3..]
            : document;

    private sealed class RegistryDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion
        {
            get;
            set;
        }

        [JsonPropertyName("hosts")]
        public HostDocument[]? Hosts
        {
            get;
            set;
        }
    }

    private sealed class HostDocument
    {
        [JsonPropertyName("profileId")]
        public string? ProfileId
        {
            get;
            set;
        }

        [JsonPropertyName("displayName")]
        public string? DisplayName
        {
            get;
            set;
        }

        [JsonPropertyName("expectedRuntimeHostId")]
        public string? ExpectedRuntimeHostId
        {
            get;
            set;
        }

        [JsonPropertyName("privateNetworkConfigurationFilePath")]
        public string? PrivateNetworkConfigurationFilePath
        {
            get;
            set;
        }

        [JsonPropertyName("enabled")]
        public bool? Enabled
        {
            get;
            set;
        }
    }
}
