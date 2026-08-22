using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Loads one strict versioned development loopback client-configuration
/// document. The document must carry the explicit development-loopback
/// profile kind so a secured private-network client configuration can never
/// be mistaken for the certificate-free development configuration.
/// </summary>
public static class RuntimeHostDevelopmentLoopbackClientOptionsFile
{
    /// <summary>
    /// The exact profile kind every development loopback client document must
    /// declare.
    /// </summary>
    public const string RequiredProfileKind =
        "development-loopback";

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

    /// <summary>
    /// Loads and validates one development loopback client configuration.
    /// </summary>
    public static async Task<RuntimeHostDevelopmentLoopbackClientOptions>
        LoadAsync(
            string filePath,
            CancellationToken cancellationToken = default)
    {
        byte[] document =
            await RuntimeHostClientConfigurationDocument.ReadBoundedAsync(
                    filePath,
                    MaximumDocumentByteCount,
                    "development loopback client",
                    cancellationToken)
                .ConfigureAwait(
                    false);

        return Parse(
            document);
    }

    private static RuntimeHostDevelopmentLoopbackClientOptions Parse(
        ReadOnlySpan<byte> document)
    {
        try
        {
            ReadOnlySpan<byte> jsonDocument =
                RuntimeHostClientConfigurationDocument.StripByteOrderMark(
                    document);
            DevelopmentClientDocument? parsedDocument =
                JsonSerializer.Deserialize<DevelopmentClientDocument>(
                    jsonDocument,
                    SerializerOptions);

            if (parsedDocument is null)
            {
                throw new InvalidDataException(
                    "The development loopback client document must contain a "
                    + "JSON object.");
            }

            if (parsedDocument.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException(
                    "The development loopback client document format version "
                    + "is not supported.");
            }

            if (parsedDocument.ProfileKind != RequiredProfileKind)
            {
                throw new InvalidDataException(
                    "The development loopback client document requires the "
                    + $"explicit profile kind '{RequiredProfileKind}'.");
            }

            if (!Uri.TryCreate(
                    parsedDocument.Address,
                    UriKind.Absolute,
                    out Uri? address))
            {
                throw new InvalidDataException(
                    "The development loopback runtime-host address is "
                    + "invalid.");
            }

            return new RuntimeHostDevelopmentLoopbackClientOptions(
                address);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The development loopback client document is not valid JSON "
                + "configuration.",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The development loopback client document contains invalid "
                + "configuration.",
                exception);
        }
    }

    private sealed class DevelopmentClientDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion
        {
            get;
            set;
        }

        [JsonPropertyName("profileKind")]
        public string? ProfileKind
        {
            get;
            set;
        }

        [JsonPropertyName("address")]
        public string? Address
        {
            get;
            set;
        }
    }
}
