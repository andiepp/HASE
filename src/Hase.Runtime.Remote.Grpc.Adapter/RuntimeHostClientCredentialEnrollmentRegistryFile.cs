using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Loads explicit mutual-TLS client enrollments from one bounded, versioned
/// JSON configuration file.
/// </summary>
public static class RuntimeHostClientCredentialEnrollmentRegistryFile
{
    private const int CurrentFormatVersion =
        1;

    private const int MaximumDocumentByteCount =
        64 * 1024;

    private const string CredentialIdPrefix =
        "x509-sha256:";

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
    /// Loads and validates the complete enrollment registry.
    /// </summary>
    public static async Task<RuntimeHostClientCredentialEnrollmentRegistry>
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
                "The client-enrollment file path must not be empty or "
                + "whitespace.",
                nameof(filePath));
        }

        if (!Path.IsPathFullyQualified(
                filePath))
        {
            throw new ArgumentException(
                "The client-enrollment file path must be fully qualified.",
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
                "The client-enrollment document exceeds the supported size.");
        }

        return Load(
            document.AsSpan(
                0,
                documentLength));
    }

    /// <summary>
    /// Loads and validates a complete enrollment registry from bounded
    /// in-memory UTF-8 JSON without accessing the file system.
    /// </summary>
    public static RuntimeHostClientCredentialEnrollmentRegistry Load(
        ReadOnlySpan<byte> document)
    {
        if (document.Length > MaximumDocumentByteCount)
        {
            throw new InvalidDataException(
                "The client-enrollment document exceeds the supported size.");
        }

        try
        {
            ReadOnlySpan<byte> jsonDocument =
                document.Length >= 3
                && document[0] == 0xEF
                && document[1] == 0xBB
                && document[2] == 0xBF
                    ? document[3..]
                    : document;

            EnrollmentDocument? parsedDocument =
                JsonSerializer.Deserialize<EnrollmentDocument>(
                    jsonDocument,
                    SerializerOptions);

            if (parsedDocument is null)
            {
                throw new InvalidDataException(
                    "The client-enrollment document must contain a JSON "
                    + "object.");
            }

            if (parsedDocument.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException(
                    "The client-enrollment document format version is not "
                    + "supported.");
            }

            if (parsedDocument.Enrollments is null
                || parsedDocument.Enrollments.Count == 0)
            {
                throw new InvalidDataException(
                    "The client-enrollment document must contain at least "
                    + "one enrollment.");
            }

            List<RuntimeHostClientCredentialEnrollment> enrollments =
                [];

            foreach (EnrollmentEntry entry
                in parsedDocument.Enrollments)
            {
                if (entry is null)
                {
                    throw new InvalidDataException(
                        "The client-enrollment document must not contain a "
                        + "null enrollment.");
                }

                enrollments.Add(
                    CreateEnrollment(
                        entry));
            }

            return new RuntimeHostClientCredentialEnrollmentRegistry(
                enrollments);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The client-enrollment document is not valid JSON "
                + "configuration.",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The client-enrollment document contains invalid or "
                + "duplicate enrollment data.",
                exception);
        }
    }

    private static RuntimeHostClientCredentialEnrollment CreateEnrollment(
        EnrollmentEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            entry.CredentialId,
            nameof(entry.CredentialId));
        ArgumentException.ThrowIfNullOrWhiteSpace(
            entry.PrincipalId,
            nameof(entry.PrincipalId));
        ArgumentException.ThrowIfNullOrWhiteSpace(
            entry.TrustPolicyId,
            nameof(entry.TrustPolicyId));

        if (!entry.CredentialId.StartsWith(
                CredentialIdPrefix,
                StringComparison.Ordinal)
            || entry.CredentialId.Length
                != CredentialIdPrefix.Length + 64
            || !entry.CredentialId
                .AsSpan(
                    CredentialIdPrefix.Length)
                .ToString()
                .All(
                    character =>
                        character is >= '0' and <= '9'
                        or >= 'a' and <= 'f'))
        {
            throw new ArgumentException(
                "The enrollment credential identifier must be a normalized "
                + "X.509 SHA-256 identifier.",
                nameof(entry.CredentialId));
        }

        return new RuntimeHostClientCredentialEnrollment(
            new RuntimeHostClientCredentialIdentity(
                RuntimeHostAuthenticationMechanism.MutualTls,
                new RuntimeHostClientCredentialId(
                    entry.CredentialId)),
            new RuntimeHostClientPrincipalId(
                entry.PrincipalId),
            entry.TrustPolicyId);
    }

    private sealed class EnrollmentDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion
        {
            get;
            set;
        }

        [JsonPropertyName("enrollments")]
        public List<EnrollmentEntry?>? Enrollments
        {
            get;
            set;
        }
    }

    private sealed class EnrollmentEntry
    {
        [JsonPropertyName("credentialId")]
        public string? CredentialId
        {
            get;
            set;
        }

        [JsonPropertyName("principalId")]
        public string? PrincipalId
        {
            get;
            set;
        }

        [JsonPropertyName("trustPolicyId")]
        public string? TrustPolicyId
        {
            get;
            set;
        }
    }
}
