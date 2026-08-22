using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Configuration;

/// <summary>
/// Loads one strict versioned Desktop Runtime Host development-profile
/// document. The document must carry the explicit development-loopback
/// profile kind so a secured installation profile can never be mistaken for
/// the certificate-free development configuration.
/// </summary>
public static class DesktopRuntimeHostDevelopmentProfileFile
{
    private const int CurrentFormatVersion = 1;
    private const string RequiredProfileKind = "development-loopback";
    private const int MaximumDocumentByteCount = 64 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    public static async Task<DesktopRuntimeHostDevelopmentProfile> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "The Desktop Runtime Host development-profile path must not be empty or whitespace.",
                nameof(filePath));
        }

        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException(
                "The Desktop Runtime Host development-profile path must be fully qualified.",
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
            throw new InvalidDataException("The Desktop Runtime Host development profile exceeds the supported size.");
        }

        return document[..length];
    }

    private static DesktopRuntimeHostDevelopmentProfile Parse(ReadOnlySpan<byte> document)
    {
        try
        {
            if (document.Length >= 3 && document[0] == 0xEF && document[1] == 0xBB && document[2] == 0xBF)
            {
                document = document[3..];
            }

            DevelopmentDocument? parsed = JsonSerializer.Deserialize<DevelopmentDocument>(document, SerializerOptions);
            if (parsed is null)
            {
                throw new InvalidDataException("The Desktop Runtime Host development profile must contain a JSON object.");
            }

            if (parsed.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException("The Desktop Runtime Host development-profile format version is not supported.");
            }

            if (parsed.ProfileKind != RequiredProfileKind)
            {
                throw new InvalidDataException(
                    "The Desktop Runtime Host development profile requires the "
                    + $"explicit profile kind '{RequiredProfileKind}'.");
            }

            string identityFilePath = parsed.IdentityFilePath
                ?? throw new InvalidDataException("The development identity file path is required.");
            string loopbackAddress = parsed.LoopbackAddress
                ?? throw new InvalidDataException("The development loopback address is required.");
            int port = parsed.Port
                ?? throw new InvalidDataException("The development listener port is required.");

            return new DesktopRuntimeHostDevelopmentProfile(
                identityFilePath,
                loopbackAddress,
                port,
                parsed.EndpointCompositionFilePath,
                parsed.IncludeByteBufferSimulation,
                ParseDiagnosticLevel(parsed.MaximumDiagnosticLevel));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Desktop Runtime Host development profile is not valid JSON configuration.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The Desktop Runtime Host development profile contains invalid configuration.", exception);
        }
    }

    private static RuntimeDiagnosticLevel ParseDiagnosticLevel(string? value) => value switch
    {
        null or nameof(RuntimeDiagnosticLevel.Operational) => RuntimeDiagnosticLevel.Operational,
        nameof(RuntimeDiagnosticLevel.Protocol) => RuntimeDiagnosticLevel.Protocol,
        nameof(RuntimeDiagnosticLevel.Bytes) => RuntimeDiagnosticLevel.Bytes,
        _ => throw new InvalidDataException("The maximum diagnostic level is invalid.")
    };

    private sealed class DevelopmentDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; }
        [JsonPropertyName("profileKind")]
        public string? ProfileKind { get; set; }
        [JsonPropertyName("identityFilePath")]
        public string? IdentityFilePath { get; set; }
        [JsonPropertyName("endpointCompositionFilePath")]
        public string? EndpointCompositionFilePath { get; set; }
        [JsonPropertyName("loopbackAddress")]
        public string? LoopbackAddress { get; set; }
        [JsonPropertyName("port")]
        public int? Port { get; set; }
        [JsonPropertyName("includeByteBufferSimulation")]
        public bool IncludeByteBufferSimulation { get; set; }
        [JsonPropertyName("maximumDiagnosticLevel")]
        public string? MaximumDiagnosticLevel { get; set; }
    }
}
