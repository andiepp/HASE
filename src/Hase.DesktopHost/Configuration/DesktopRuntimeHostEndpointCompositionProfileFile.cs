using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hase.DesktopHost.Configuration;

public static class DesktopRuntimeHostEndpointCompositionProfileFile
{
    private const int LegacyFormatVersion =
        DesktopRuntimeHostEndpointCompositionProfile.LegacyFormatVersion;

    private const int OpenFormatVersion =
        DesktopRuntimeHostEndpointCompositionProfile.OpenFormatVersion;

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

            return ReadFormatVersion(document) switch
            {
                LegacyFormatVersion => ParseLegacy(document),
                OpenFormatVersion => ParseOpen(document),
                _ => throw new InvalidDataException(
                    "The endpoint-composition profile format version is not supported.")
            };
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

    /// <summary>
    /// Reads only the declared version, so that each shape is then parsed by
    /// the reader written for it.
    /// </summary>
    private static int ReadFormatVersion(ReadOnlySpan<byte> document)
    {
        var reader = new Utf8JsonReader(document, new JsonReaderOptions { MaxDepth = 12 });
        using JsonDocument parsed = JsonDocument.ParseValue(ref reader);

        if (parsed.RootElement.ValueKind is not JsonValueKind.Object)
        {
            throw new InvalidDataException("The endpoint-composition profile must contain a JSON object.");
        }

        if (!parsed.RootElement.TryGetProperty("formatVersion", out JsonElement version)
            || version.ValueKind is not JsonValueKind.Number
            || !version.TryGetInt32(out int formatVersion))
        {
            throw new InvalidDataException("The endpoint-composition profile requires a numeric formatVersion.");
        }

        return formatVersion;
    }

    private static DesktopRuntimeHostEndpointCompositionProfile ParseLegacy(
        ReadOnlySpan<byte> document)
    {
        CompositionDocument? parsed = JsonSerializer.Deserialize<CompositionDocument>(document, SerializerOptions);

        if (parsed is null)
        {
            throw new InvalidDataException("The endpoint-composition profile must contain a JSON object.");
        }

        EndpointDocument[] endpoints = parsed.Endpoints
            ?? throw new InvalidDataException("The endpoints collection is required.");

        if (endpoints.Any(endpoint =>
                endpoint.Kind is not ("NativeNetwork" or "CompactSerial" or "Kel103Serial" or "RfLabSerial")))
        {
            throw new InvalidDataException("An endpoint kind is missing or unsupported.");
        }

        return new DesktopRuntimeHostEndpointCompositionProfile(
            endpoints.Where(endpoint => endpoint.Kind == "NativeNetwork").Select(CreateNativeEndpoint),
            endpoints.Where(endpoint => endpoint.Kind == "CompactSerial").Select(CreateCompactEndpoint),
            endpoints.Where(endpoint => endpoint.Kind == "Kel103Serial").Select(CreateKel103Endpoint),
            endpoints.Where(endpoint => endpoint.Kind == "RfLabSerial").Select(CreateRfLabEndpoint))
        {
            FormatVersion = LegacyFormatVersion
        };
    }

    /// <summary>
    /// Reads the provider-keyed shape. No provider identifier is checked
    /// against a list here: the composition names what supplies an endpoint,
    /// and only that provider knows what its settings mean.
    /// </summary>
    private static DesktopRuntimeHostEndpointCompositionProfile ParseOpen(
        ReadOnlySpan<byte> document)
    {
        OpenCompositionDocument? parsed =
            JsonSerializer.Deserialize<OpenCompositionDocument>(document, SerializerOptions);

        if (parsed is null)
        {
            throw new InvalidDataException("The endpoint-composition profile must contain a JSON object.");
        }

        OpenEndpointDocument[] endpoints = parsed.Endpoints
            ?? throw new InvalidDataException("The endpoints collection is required.");

        return new DesktopRuntimeHostEndpointCompositionProfile(
            endpoints.Select(CreateProviderEndpoint))
        {
            FormatVersion = OpenFormatVersion
        };
    }

    private static DesktopRuntimeHostEndpointEntry CreateProviderEndpoint(
        OpenEndpointDocument endpoint)
    {
        string providerId = Required(endpoint.ProviderId, "providerId");
        string expectedEndpointId = Required(endpoint.ExpectedEndpointId, "expectedEndpointId");
        var settings = new List<KeyValuePair<string, string>>();

        foreach (KeyValuePair<string, JsonElement> setting in endpoint.Settings ?? [])
        {
            settings.Add(
                new KeyValuePair<string, string>(
                    setting.Key,
                    ReadSettingText(expectedEndpointId, setting.Key, setting.Value)));
        }

        return new DesktopRuntimeHostEndpointEntry(
            providerId,
            expectedEndpointId,
            settings);
    }

    private static string ReadSettingText(
        string expectedEndpointId,
        string name,
        JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String =>
                value.GetString() ?? string.Empty,
            JsonValueKind.Number =>
                value.GetRawText(),
            JsonValueKind.True =>
                bool.TrueString,
            JsonValueKind.False =>
                bool.FalseString,
            _ => throw new InvalidDataException(
                $"Endpoint '{expectedEndpointId}' setting '{name}' must be "
                + "text, a number, or a boolean.")
        };

    private static DesktopRuntimeHostNativeNetworkEndpointProfile CreateNativeEndpoint(EndpointDocument endpoint)
    {
        if (endpoint.VendorId is not null || endpoint.ProductId is not null
            || endpoint.BaudRate is not null || endpoint.VerificationTimeoutMilliseconds is not null
            || HasKel103Properties(endpoint))
        {
            throw new InvalidDataException("A native network endpoint contains serial-endpoint properties.");
        }

        return new DesktopRuntimeHostNativeNetworkEndpointProfile(
            Required(endpoint.ExpectedEndpointId, "expectedEndpointId"),
            Required(endpoint.Host, "host"),
            endpoint.Port ?? throw new InvalidDataException("A native network endpoint requires port."));
    }

    private static DesktopRuntimeHostCompactSerialEndpointProfile CreateCompactEndpoint(EndpointDocument endpoint)
    {
        if (endpoint.Host is not null || endpoint.Port is not null || HasKel103Properties(endpoint))
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

    private static DesktopRuntimeHostKel103SerialEndpointProfile CreateKel103Endpoint(EndpointDocument endpoint)
    {
        if (endpoint.Host is not null || endpoint.Port is not null
            || endpoint.VendorId is not null || endpoint.ProductId is not null
            || endpoint.VerificationTimeoutMilliseconds is not null)
        {
            throw new InvalidDataException("A KEL-103 serial endpoint contains properties from another endpoint family.");
        }

        return new DesktopRuntimeHostKel103SerialEndpointProfile(
            Required(endpoint.ExpectedEndpointId, "expectedEndpointId"),
            Required(endpoint.DefinitionId, "definitionId"),
            endpoint.DefinitionVersion
                ?? throw new InvalidDataException("A KEL-103 serial endpoint requires definitionVersion."),
            Required(endpoint.SerialPort, "serialPort"),
            endpoint.BaudRate
                ?? throw new InvalidDataException("A KEL-103 serial endpoint requires baudRate."));
    }

    private static DesktopRuntimeHostRfLabSerialEndpointProfile CreateRfLabEndpoint(EndpointDocument endpoint)
    {
        if (endpoint.Host is not null || endpoint.Port is not null
            || endpoint.VendorId is not null || endpoint.ProductId is not null
            || endpoint.VerificationTimeoutMilliseconds is not null)
        {
            throw new InvalidDataException("An RF-Lab serial endpoint contains properties from another endpoint family.");
        }

        return new DesktopRuntimeHostRfLabSerialEndpointProfile(
            Required(endpoint.ExpectedEndpointId, "expectedEndpointId"),
            Required(endpoint.DefinitionId, "definitionId"),
            endpoint.DefinitionVersion
                ?? throw new InvalidDataException("An RF-Lab serial endpoint requires definitionVersion."),
            Required(endpoint.SerialPort, "serialPort"),
            endpoint.BaudRate
                ?? throw new InvalidDataException("An RF-Lab serial endpoint requires baudRate."));
    }

    private static bool HasKel103Properties(EndpointDocument endpoint) =>
        endpoint.DefinitionId is not null
        || endpoint.DefinitionVersion is not null
        || endpoint.SerialPort is not null;

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
        [JsonPropertyName("definitionId")]
        public string? DefinitionId { get; set; }
        [JsonPropertyName("definitionVersion")]
        public ushort? DefinitionVersion { get; set; }
        [JsonPropertyName("serialPort")]
        public string? SerialPort { get; set; }
    }

    private sealed class OpenCompositionDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; }

        [JsonPropertyName("endpoints")]
        public OpenEndpointDocument[]? Endpoints { get; set; }
    }

    private sealed class OpenEndpointDocument
    {
        [JsonPropertyName("providerId")]
        public string? ProviderId { get; set; }

        [JsonPropertyName("expectedEndpointId")]
        public string? ExpectedEndpointId { get; set; }

        [JsonPropertyName("settings")]
        public Dictionary<string, JsonElement>? Settings { get; set; }
    }
}
