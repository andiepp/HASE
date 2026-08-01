using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hase.DesktopHost.Configuration;

public static class DesktopRuntimeHostEndpointCompositionProfileFile
{
    private const int CurrentFormatVersion = 1;
    private const int MaximumDocumentByteCount = 64 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };

    public static async Task<DesktopRuntimeHostEndpointCompositionProfile> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException(
                "The endpoint-composition profile path must be fully qualified.",
                nameof(filePath));
        }

        cancellationToken.ThrowIfCancellationRequested();
        byte[] document = await ReadBoundedDocumentAsync(
                Path.GetFullPath(filePath),
                cancellationToken)
            .ConfigureAwait(false);

        return Parse(document);
    }

    private static async Task<byte[]> ReadBoundedDocumentAsync(
        string filePath,
        CancellationToken cancellationToken)
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
            int read = await stream.ReadAsync(document.AsMemory(length), cancellationToken)
                .ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            length += read;
        }

        if (length > MaximumDocumentByteCount)
        {
            throw new InvalidDataException("The endpoint-composition profile exceeds the supported size.");
        }

        return document[..length];
    }

    private static DesktopRuntimeHostEndpointCompositionProfile Parse(ReadOnlySpan<byte> document)
    {
        try
        {
            if (document.Length >= 3 && document[0] == 0xEF && document[1] == 0xBB && document[2] == 0xBF)
            {
                document = document[3..];
            }

            CompositionDocument? parsed = JsonSerializer.Deserialize<CompositionDocument>(document, SerializerOptions);

            if (parsed is null)
            {
                throw new InvalidDataException("The endpoint-composition profile must contain a JSON object.");
            }

            if (parsed.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException("The endpoint-composition profile format version is not supported.");
            }

            EndpointDocument[] endpoints = parsed.Endpoints
                ?? throw new InvalidDataException("The endpoints collection is required.");

            if (endpoints.Any(endpoint => endpoint.Kind is not ("NativeNetwork" or "CompactSerial")))
            {
                throw new InvalidDataException("An endpoint kind is missing or unsupported.");
            }

            return new DesktopRuntimeHostEndpointCompositionProfile(
                endpoints.Where(endpoint => endpoint.Kind == "NativeNetwork").Select(CreateNativeEndpoint),
                endpoints.Where(endpoint => endpoint.Kind == "CompactSerial").Select(CreateCompactEndpoint));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The endpoint-composition profile is not valid JSON configuration.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The endpoint-composition profile contains invalid configuration.", exception);
        }
    }

    private static DesktopRuntimeHostNativeNetworkEndpointProfile CreateNativeEndpoint(EndpointDocument endpoint)
    {
        RejectCompactProperties(endpoint);
        return new DesktopRuntimeHostNativeNetworkEndpointProfile(
            Required(endpoint.ExpectedEndpointId, "expectedEndpointId"),
            Required(endpoint.Host, "host"),
            endpoint.Port ?? throw new InvalidDataException("A native network endpoint requires port."));
    }

    private static DesktopRuntimeHostCompactSerialEndpointProfile CreateCompactEndpoint(EndpointDocument endpoint)
    {
        if (endpoint.Host is not null || endpoint.Port is not null)
        {
            throw new InvalidDataException("A compact serial endpoint contains native-network properties.");
        }

        return new DesktopRuntimeHostCompactSerialEndpointProfile(
            Required(endpoint.ExpectedEndpointId, "expectedEndpointId"),
            endpoint.VendorId ?? throw new InvalidDataException("A compact serial endpoint requires vendorId."),
            endpoint.ProductId ?? throw new InvalidDataException("A compact serial endpoint requires productId."),
            endpoint.BaudRate ?? throw new InvalidDataException("A compact serial endpoint requires baudRate."),
            TimeSpan.FromMilliseconds(endpoint.VerificationTimeoutMilliseconds
                ?? throw new InvalidDataException("A compact serial endpoint requires verificationTimeoutMilliseconds.")));
    }

    private static void RejectCompactProperties(EndpointDocument endpoint)
    {
        if (endpoint.VendorId is not null || endpoint.ProductId is not null
            || endpoint.BaudRate is not null || endpoint.VerificationTimeoutMilliseconds is not null)
        {
            throw new InvalidDataException("A native network endpoint contains compact-serial properties.");
        }
    }

    private static string Required(string? value, string propertyName) =>
        value ?? throw new InvalidDataException($"Endpoint property '{propertyName}' is required.");

    private sealed class CompositionDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; }

        [JsonPropertyName("endpoints")]
        public EndpointDocument[]? Endpoints { get; set; }
    }

    private sealed class EndpointDocument
    {
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }
        [JsonPropertyName("expectedEndpointId")]
        public string? ExpectedEndpointId { get; set; }
        [JsonPropertyName("host")]
        public string? Host { get; set; }
        [JsonPropertyName("port")]
        public int? Port { get; set; }
        [JsonPropertyName("vendorId")]
        public ushort? VendorId { get; set; }
        [JsonPropertyName("productId")]
        public ushort? ProductId { get; set; }
        [JsonPropertyName("baudRate")]
        public int? BaudRate { get; set; }
        [JsonPropertyName("verificationTimeoutMilliseconds")]
        public int? VerificationTimeoutMilliseconds { get; set; }
    }
}
