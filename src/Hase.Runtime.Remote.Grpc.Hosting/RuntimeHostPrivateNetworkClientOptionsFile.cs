using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Loads one bounded, versioned private-network client configuration.
/// </summary>
public static class RuntimeHostPrivateNetworkClientOptionsFile
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

    /// <summary>
    /// Loads and validates one complete private-network client configuration.
    /// </summary>
    public static async Task<RuntimeHostPrivateNetworkClientOptions> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            filePath);

        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            throw new ArgumentException(
                "The private-network client file path must not be empty or "
                + "whitespace.",
                nameof(filePath));
        }

        if (!Path.IsPathFullyQualified(
                filePath))
        {
            throw new ArgumentException(
                "The private-network client file path must be fully "
                + "qualified.",
                nameof(filePath));
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using FileStream stream =
            new(
                Path.GetFullPath(
                    filePath),
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
                "The private-network client document exceeds the supported "
                + "size.");
        }

        return Parse(
            document.AsSpan(
                0,
                documentLength));
    }

    private static RuntimeHostPrivateNetworkClientOptions Parse(
        ReadOnlySpan<byte> document)
    {
        try
        {
            ReadOnlySpan<byte> jsonDocument =
                document.Length >= 3
                && document[0] == 0xEF
                && document[1] == 0xBB
                && document[2] == 0xBF
                    ? document[3..]
                    : document;
            ClientDocument? parsedDocument =
                JsonSerializer.Deserialize<ClientDocument>(
                    jsonDocument,
                    SerializerOptions);

            if (parsedDocument is null)
            {
                throw new InvalidDataException(
                    "The private-network client document must contain a "
                    + "JSON object.");
            }

            if (parsedDocument.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException(
                    "The private-network client document format version is "
                    + "not supported.");
            }

            if (parsedDocument.ClientCertificate is null
                || parsedDocument.TrustedServerCertificate is null)
            {
                throw new InvalidDataException(
                    "The private-network client document is incomplete.");
            }

            if (!Uri.TryCreate(
                    parsedDocument.Address,
                    UriKind.Absolute,
                    out Uri? address))
            {
                throw new InvalidDataException(
                    "The private-network runtime-host address is invalid.");
            }

            return new RuntimeHostPrivateNetworkClientOptions(
                address,
                CreateCertificateReference(
                    parsedDocument.ClientCertificate,
                    "client"),
                CreateCertificateReference(
                    parsedDocument.TrustedServerCertificate,
                    "trusted server"));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The private-network client document is not valid JSON "
                + "configuration.",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The private-network client document contains invalid "
                + "configuration.",
                exception);
        }
    }

    private static RuntimeHostCertificateStoreReference
        CreateCertificateReference(
            CertificateDocument document,
            string role)
    {
        if (!Enum.TryParse(
                document.StoreName,
                ignoreCase: false,
                out StoreName storeName)
            || !Enum.IsDefined(
                storeName))
        {
            throw new InvalidDataException(
                $"The {role} certificate store name is invalid.");
        }

        if (!Enum.TryParse(
                document.StoreLocation,
                ignoreCase: false,
                out StoreLocation storeLocation)
            || !Enum.IsDefined(
                storeLocation))
        {
            throw new InvalidDataException(
                $"The {role} certificate store location is invalid.");
        }

        return new RuntimeHostCertificateStoreReference(
            storeName,
            storeLocation,
            document.Thumbprint
            ?? throw new InvalidDataException(
                $"The {role} certificate thumbprint is required."));
    }

    private sealed class ClientDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion
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

        [JsonPropertyName("clientCertificate")]
        public CertificateDocument? ClientCertificate
        {
            get;
            set;
        }

        [JsonPropertyName("trustedServerCertificate")]
        public CertificateDocument? TrustedServerCertificate
        {
            get;
            set;
        }
    }

    private sealed class CertificateDocument
    {
        [JsonPropertyName("storeName")]
        public string? StoreName
        {
            get;
            set;
        }

        [JsonPropertyName("storeLocation")]
        public string? StoreLocation
        {
            get;
            set;
        }

        [JsonPropertyName("thumbprint")]
        public string? Thumbprint
        {
            get;
            set;
        }
    }
}
