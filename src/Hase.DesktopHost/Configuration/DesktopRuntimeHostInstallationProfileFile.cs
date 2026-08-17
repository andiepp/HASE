using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Configuration;

public static class DesktopRuntimeHostInstallationProfileFile
{
    private const int CurrentFormatVersion = 1;
    private const int MaximumDocumentByteCount = 64 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    public static async Task<DesktopRuntimeHostInstallationProfile> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "The Desktop Runtime Host application-profile path must not be empty or whitespace.",
                nameof(filePath));
        }

        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException(
                "The Desktop Runtime Host application-profile path must be fully qualified.",
                nameof(filePath));
        }

        cancellationToken.ThrowIfCancellationRequested();
        byte[] document = await ReadBoundedDocumentAsync(Path.GetFullPath(filePath), cancellationToken)
            .ConfigureAwait(false);
        return Parse(document);
    }

    private static async Task<byte[]> ReadBoundedDocumentAsync(string filePath, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] document = new byte[MaximumDocumentByteCount + 1];
        int length = 0;

        while (length < document.Length)
        {
            int read = await stream.ReadAsync(document.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        if (length > MaximumDocumentByteCount)
        {
            throw new InvalidDataException("The Desktop Runtime Host application profile exceeds the supported size.");
        }

        return document[..length];
    }

    private static DesktopRuntimeHostInstallationProfile Parse(ReadOnlySpan<byte> document)
    {
        try
        {
            if (document.Length >= 3 && document[0] == 0xEF && document[1] == 0xBB && document[2] == 0xBF)
            {
                document = document[3..];
            }

            InstallationDocument? parsed = JsonSerializer.Deserialize<InstallationDocument>(document, SerializerOptions);
            if (parsed is null)
            {
                throw new InvalidDataException("The Desktop Runtime Host application profile must contain a JSON object.");
            }

            if (parsed.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException("The Desktop Runtime Host application-profile format version is not supported.");
            }

            string identityFilePath = parsed.IdentityFilePath
                ?? throw new InvalidDataException("The installation identity file path is required.");
            string privateNetworkFilePath = parsed.PrivateNetworkConfigurationFilePath
                ?? throw new InvalidDataException("The private-network deployment configuration file path is required.");

            return parsed.EndpointCompositionFilePath is null
                ? new DesktopRuntimeHostInstallationProfile(
                    identityFilePath,
                    privateNetworkFilePath,
                    ParseDiagnosticLevel(parsed.MaximumDiagnosticLevel),
                    parsed.IncludeByteBufferSimulation,
                    parsed.RemoteDiagnosticsEnabled,
                    ParseRemoteDiagnosticLevel(
                        parsed.RemoteDiagnosticsMaximumLevel),
                    parsed.AuthorizationPolicyFilePath,
                    parsed.MediaConfigurationFilePath)
                : new DesktopRuntimeHostInstallationProfile(
                    identityFilePath,
                    privateNetworkFilePath,
                    parsed.EndpointCompositionFilePath,
                    ParseDiagnosticLevel(parsed.MaximumDiagnosticLevel),
                    parsed.IncludeByteBufferSimulation,
                    parsed.RemoteDiagnosticsEnabled,
                    ParseRemoteDiagnosticLevel(
                        parsed.RemoteDiagnosticsMaximumLevel),
                    parsed.AuthorizationPolicyFilePath,
                    parsed.MediaConfigurationFilePath);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Desktop Runtime Host application profile is not valid JSON configuration.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The Desktop Runtime Host application profile contains invalid configuration.", exception);
        }
    }

    private static RuntimeDiagnosticLevel ParseDiagnosticLevel(string? value) => value switch
    {
        null or nameof(RuntimeDiagnosticLevel.Operational) => RuntimeDiagnosticLevel.Operational,
        nameof(RuntimeDiagnosticLevel.Protocol) => RuntimeDiagnosticLevel.Protocol,
        nameof(RuntimeDiagnosticLevel.Bytes) => RuntimeDiagnosticLevel.Bytes,
        _ => throw new InvalidDataException("The maximum diagnostic level is invalid.")
    };

    private static RuntimeDiagnosticLevel ParseRemoteDiagnosticLevel(
        string? value) => value switch
    {
        null or nameof(RuntimeDiagnosticLevel.Operational) =>
            RuntimeDiagnosticLevel.Operational,
        nameof(RuntimeDiagnosticLevel.Protocol) =>
            RuntimeDiagnosticLevel.Protocol,
        nameof(RuntimeDiagnosticLevel.Bytes) =>
            RuntimeDiagnosticLevel.Bytes,
        _ => throw new InvalidDataException(
            "The remote diagnostics maximum level is invalid.")
    };

    private sealed class InstallationDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; }
        [JsonPropertyName("identityFilePath")]
        public string? IdentityFilePath { get; set; }
        [JsonPropertyName("privateNetworkConfigurationFilePath")]
        public string? PrivateNetworkConfigurationFilePath { get; set; }
        [JsonPropertyName("endpointCompositionFilePath")]
        public string? EndpointCompositionFilePath { get; set; }
        [JsonPropertyName("maximumDiagnosticLevel")]
        public string? MaximumDiagnosticLevel { get; set; }
        [JsonPropertyName("includeByteBufferSimulation")]
        public bool IncludeByteBufferSimulation { get; set; }
        [JsonPropertyName("remoteDiagnosticsEnabled")]
        public bool RemoteDiagnosticsEnabled { get; set; }
        [JsonPropertyName("remoteDiagnosticsMaximumLevel")]
        public string? RemoteDiagnosticsMaximumLevel { get; set; }
        [JsonPropertyName("authorizationPolicyFilePath")]
        public string? AuthorizationPolicyFilePath { get; set; }
        [JsonPropertyName("mediaConfigurationFilePath")]
        public string? MediaConfigurationFilePath { get; set; }
    }
}
