using System.Net;
using System.Text.Json;

namespace Hase.Python.CredentialProvisioning;

/// <summary>
/// Represents the strict, version-1 Python Runtime Host profile document.
/// Credential-file availability is deliberately outside this document-only
/// boundary so unpublished candidate paths can be validated in memory.
/// </summary>
public sealed record PythonRuntimeHostProfileDocument(
    string Address,
    string ClientCertificateChainPath,
    string ClientPrivateKeyPath,
    string TrustedServerCertificatePath)
{
    private const int MaximumDocumentByteCount = 64 * 1024;

    /// <summary>
    /// Parses and validates bounded UTF-8 JSON without file-system access.
    /// </summary>
    public static PythonRuntimeHostProfileDocument Load(
        ReadOnlySpan<byte> document)
    {
        if (document.Length > MaximumDocumentByteCount)
        {
            throw new InvalidDataException(
                "The Python Runtime Host profile exceeds the supported size.");
        }

        try
        {
            var reader = new Utf8JsonReader(document);
            using JsonDocument parsed = JsonDocument.ParseValue(ref reader);
            if (reader.Read())
            {
                throw Invalid();
            }
            RejectDuplicateProperties(parsed.RootElement);
            JsonElement root = parsed.RootElement;
            RequireProperties(
                root,
                "formatVersion",
                "address",
                "clientCertificate",
                "trustedServerCertificate");

            if (root.GetProperty("formatVersion").ValueKind
                    != JsonValueKind.Number
                || root.GetProperty("formatVersion").GetInt32() != 1)
            {
                throw Invalid();
            }

            JsonElement client = root.GetProperty("clientCertificate");
            JsonElement trusted = root.GetProperty("trustedServerCertificate");
            RequireProperties(client, "certificateChainPath", "privateKeyPath");
            RequireProperties(trusted, "certificatePath");

            string address = RequireString(root.GetProperty("address"));
            string certificate = RequireAbsolutePath(
                client.GetProperty("certificateChainPath"));
            string privateKey = RequireAbsolutePath(
                client.GetProperty("privateKeyPath"));
            string trustedCertificate = RequireAbsolutePath(
                trusted.GetProperty("certificatePath"));

            if (!IsStrictAddress(address)
                || new[] { certificate, privateKey, trustedCertificate }
                    .Select(Path.GetFullPath)
                    .Distinct(PathComparer)
                    .Count() != 3)
            {
                throw Invalid();
            }

            return new PythonRuntimeHostProfileDocument(
                address,
                Path.GetFullPath(certificate),
                Path.GetFullPath(privateKey),
                Path.GetFullPath(trustedCertificate));
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException
            or InvalidOperationException
            or FormatException
            or ArgumentException
            or NotSupportedException)
        {
            throw Invalid(exception);
        }
    }

    /// <summary>
    /// Serializes this profile and validates the resulting candidate through
    /// the same authoritative in-memory loading path.
    /// </summary>
    public byte[] Serialize()
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                formatVersion = 1,
                address = Address,
                clientCertificate = new
                {
                    certificateChainPath = ClientCertificateChainPath,
                    privateKeyPath = ClientPrivateKeyPath
                },
                trustedServerCertificate = new
                {
                    certificatePath = TrustedServerCertificatePath
                }
            },
            new JsonSerializerOptions { WriteIndented = true });

        _ = Load(bytes);
        return bytes;
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw Invalid();
                }
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item);
            }
        }
    }

    private static void RequireProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.EnumerateObject().Select(property => property.Name)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(names.Order(StringComparer.Ordinal)))
        {
            throw Invalid();
        }
    }

    private static string RequireString(JsonElement element)
    {
        string? value = element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw Invalid();
        }
        return value;
    }

    private static string RequireAbsolutePath(JsonElement element)
    {
        string value = RequireString(element);
        if (!Path.IsPathFullyQualified(value))
        {
            throw Invalid();
        }
        return value;
    }

    private static bool IsStrictAddress(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.IsDefaultPort
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || address != $"https://{uri.Authority}")
        {
            return false;
        }

        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out _);
    }

    private static InvalidDataException Invalid(Exception? inner = null) =>
        new("The Python Runtime Host profile document is invalid.", inner);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
}
