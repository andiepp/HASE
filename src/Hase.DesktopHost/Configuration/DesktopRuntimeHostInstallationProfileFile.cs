using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Configuration;

/// <summary>
/// Loads one bounded, versioned Desktop Runtime Host application profile.
/// </summary>
public static class DesktopRuntimeHostInstallationProfileFile
{
    private const int CurrentFormatVersion =
        1;

    private const int MaximumDocumentByteCount =
        64 * 1024;

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

    public static async Task<DesktopRuntimeHostInstallationProfile> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            filePath);

        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            throw new ArgumentException(
                "The Desktop Runtime Host application-profile path must not be empty or whitespace.",
                nameof(filePath));
        }

        if (!Path.IsPathFullyQualified(
                filePath))
        {
            throw new ArgumentException(
                "The Desktop Runtime Host application-profile path must be fully qualified.",
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
                "The Desktop Runtime Host application profile exceeds the supported size.");
        }

        return document[
            ..documentLength];
    }

    private static DesktopRuntimeHostInstallationProfile Parse(
        ReadOnlySpan<byte> document)
    {
        try
        {
            ReadOnlySpan<byte> jsonDocument =
                RemoveUtf8ByteOrderMark(
                    document);
            InstallationDocument? parsedDocument =
                JsonSerializer.Deserialize<InstallationDocument>(
                    jsonDocument,
                    SerializerOptions);

            if (parsedDocument is null)
            {
                throw new InvalidDataException(
                    "The Desktop Runtime Host application profile must contain a JSON object.");
            }

            if (parsedDocument.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException(
                    "The Desktop Runtime Host application-profile format version is not supported.");
            }

            RuntimeDiagnosticLevel diagnosticLevel =
                ParseDiagnosticLevel(
                    parsedDocument.MaximumDiagnosticLevel);

            return new DesktopRuntimeHostInstallationProfile(
                parsedDocument.IdentityFilePath
                ?? throw new InvalidDataException(
                    "The installation identity file path is required."),
                parsedDocument.PrivateNetworkConfigurationFilePath
                ?? throw new InvalidDataException(
                    "The private-network deployment configuration file path is required."),
                diagnosticLevel,
                parsedDocument.IncludeByteBufferSimulation);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The Desktop Runtime Host application profile is not valid JSON configuration.",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The Desktop Runtime Host application profile contains invalid configuration.",
                exception);
        }
    }

    private static RuntimeDiagnosticLevel ParseDiagnosticLevel(
        string? value) =>
        value switch
        {
            null =>
                RuntimeDiagnosticLevel.Operational,
            nameof(RuntimeDiagnosticLevel.Operational) =>
                RuntimeDiagnosticLevel.Operational,
            nameof(RuntimeDiagnosticLevel.Protocol) =>
                RuntimeDiagnosticLevel.Protocol,
            nameof(RuntimeDiagnosticLevel.Bytes) =>
                RuntimeDiagnosticLevel.Bytes,
            _ =>
                throw new InvalidDataException(
                    "The maximum diagnostic level is invalid.")
        };

    private static ReadOnlySpan<byte> RemoveUtf8ByteOrderMark(
        ReadOnlySpan<byte> document) =>
        document.Length >= 3
        && document[0] == 0xEF
        && document[1] == 0xBB
        && document[2] == 0xBF
            ? document[3..]
            : document;

    private sealed class InstallationDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion
        {
            get;
            set;
        }

        [JsonPropertyName("identityFilePath")]
        public string? IdentityFilePath
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

        [JsonPropertyName("maximumDiagnosticLevel")]
        public string? MaximumDiagnosticLevel
        {
            get;
            set;
        }

        [JsonPropertyName("includeByteBufferSimulation")]
        public bool IncludeByteBufferSimulation
        {
            get;
            set;
        }
    }
}
