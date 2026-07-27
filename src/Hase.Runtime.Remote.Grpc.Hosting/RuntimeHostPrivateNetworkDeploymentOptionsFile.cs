using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Loads one bounded, versioned private-network deployment configuration.
/// </summary>
public static class RuntimeHostPrivateNetworkDeploymentOptionsFile
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
    /// Loads and validates one complete deployment configuration.
    /// </summary>
    public static async Task<RuntimeHostPrivateNetworkDeploymentOptions>
        LoadAsync(
            string filePath,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            filePath);

        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            throw new ArgumentException(
                "The private-network deployment file path must not be empty "
                + "or whitespace.",
                nameof(filePath));
        }

        if (!Path.IsPathFullyQualified(
                filePath))
        {
            throw new ArgumentException(
                "The private-network deployment file path must be fully "
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
                "The private-network deployment document exceeds the "
                + "supported size.");
        }

        return Parse(
            document.AsSpan(
                0,
                documentLength));
    }

    private static RuntimeHostPrivateNetworkDeploymentOptions Parse(
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
            DeploymentDocument? parsedDocument =
                JsonSerializer.Deserialize<DeploymentDocument>(
                    jsonDocument,
                    SerializerOptions);

            if (parsedDocument is null)
            {
                throw new InvalidDataException(
                    "The private-network deployment document must contain "
                    + "a JSON object.");
            }

            if (parsedDocument.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException(
                    "The private-network deployment document format version "
                    + "is not supported.");
            }

            if (parsedDocument.Binding is null
                || parsedDocument.ServerCertificate is null)
            {
                throw new InvalidDataException(
                    "The private-network deployment document is incomplete.");
            }

            if (!IPAddress.TryParse(
                    parsedDocument.Binding.Address,
                    out IPAddress? address))
            {
                throw new InvalidDataException(
                    "The private-network listener address must be an explicit "
                    + "IP address.");
            }

            if (!Enum.TryParse(
                    parsedDocument.ServerCertificate.StoreName,
                    ignoreCase: false,
                    out StoreName storeName)
                || !Enum.IsDefined(
                    storeName))
            {
                throw new InvalidDataException(
                    "The server-certificate store name is invalid.");
            }

            if (!Enum.TryParse(
                    parsedDocument.ServerCertificate.StoreLocation,
                    ignoreCase: false,
                    out StoreLocation storeLocation)
                || !Enum.IsDefined(
                    storeLocation))
            {
                throw new InvalidDataException(
                    "The server-certificate store location is invalid.");
            }

            return new RuntimeHostPrivateNetworkDeploymentOptions(
                new PrivateNetworkGrpcBinding(
                    address,
                    parsedDocument.Binding.Port),
                new RuntimeHostCertificateStoreReference(
                    storeName,
                    storeLocation,
                    parsedDocument.ServerCertificate.Thumbprint
                    ?? throw new InvalidDataException(
                        "The server-certificate thumbprint is required.")),
                parsedDocument.ClientEnrollmentFilePath
                ?? throw new InvalidDataException(
                    "The client-enrollment file path is required."));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The private-network deployment document is not valid JSON "
                + "configuration.",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The private-network deployment document contains invalid "
                + "configuration.",
                exception);
        }
    }

    private sealed class DeploymentDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion
        {
            get;
            set;
        }

        [JsonPropertyName("binding")]
        public BindingDocument? Binding
        {
            get;
            set;
        }

        [JsonPropertyName("serverCertificate")]
        public CertificateDocument? ServerCertificate
        {
            get;
            set;
        }

        [JsonPropertyName("clientEnrollmentFilePath")]
        public string? ClientEnrollmentFilePath
        {
            get;
            set;
        }
    }

    private sealed class BindingDocument
    {
        [JsonPropertyName("address")]
        public string? Address
        {
            get;
            set;
        }

        [JsonPropertyName("port")]
        public int Port
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
