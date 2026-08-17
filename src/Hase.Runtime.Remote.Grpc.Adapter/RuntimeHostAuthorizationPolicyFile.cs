using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Loads explicit northbound authorization grants from one bounded,
/// versioned JSON configuration file.
/// </summary>
public static class RuntimeHostAuthorizationPolicyFile
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
    /// Loads and validates the complete authorization policy.
    /// </summary>
    public static async Task<RuntimeHostAuthorizationPolicy> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            filePath);

        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            throw new ArgumentException(
                "The authorization-policy file path must not be empty or "
                + "whitespace.",
                nameof(filePath));
        }

        if (!Path.IsPathFullyQualified(
                filePath))
        {
            throw new ArgumentException(
                "The authorization-policy file path must be fully qualified.",
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
                "The authorization-policy document exceeds the supported "
                + "size.");
        }

        return Load(
            document.AsSpan(
                0,
                documentLength));
    }

    /// <summary>
    /// Loads and validates a complete authorization policy from bounded
    /// in-memory UTF-8 JSON without accessing the file system.
    /// </summary>
    public static RuntimeHostAuthorizationPolicy Load(
        ReadOnlySpan<byte> document)
    {
        if (document.Length > MaximumDocumentByteCount)
        {
            throw new InvalidDataException(
                "The authorization-policy document exceeds the supported "
                + "size.");
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

            AuthorizationDocument? parsedDocument =
                JsonSerializer.Deserialize<AuthorizationDocument>(
                    jsonDocument,
                    SerializerOptions);

            if (parsedDocument is null)
            {
                throw new InvalidDataException(
                    "The authorization-policy document must contain a JSON "
                    + "object.");
            }

            if (parsedDocument.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException(
                    "The authorization-policy document format version is not "
                    + "supported.");
            }

            if (parsedDocument.Grants is null)
            {
                throw new InvalidDataException(
                    "The authorization-policy document must contain a grants "
                    + "collection.");
            }

            List<RuntimeHostPermissionGrant> grants =
                [];
            HashSet<RuntimeHostPermissionGrant> distinctGrants =
                [];

            foreach (AuthorizationGrantEntry? entry in parsedDocument.Grants)
            {
                if (entry is null)
                {
                    throw new InvalidDataException(
                        "The authorization-policy document must not contain a "
                        + "null grant.");
                }

                RuntimeHostPermissionGrant grant =
                    CreateGrant(
                        entry);

                if (!distinctGrants.Add(
                        grant))
                {
                    throw new InvalidDataException(
                        "The authorization-policy document contains a "
                        + "duplicate grant.");
                }

                grants.Add(
                    grant);
            }

            return new RuntimeHostAuthorizationPolicy(
                grants);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The authorization-policy document is not valid JSON "
                + "configuration.",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The authorization-policy document contains invalid grant "
                + "data.",
                exception);
        }
    }

    private static RuntimeHostPermissionGrant CreateGrant(
        AuthorizationGrantEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            entry.PrincipalId,
            nameof(entry.PrincipalId));
        ArgumentException.ThrowIfNullOrWhiteSpace(
            entry.Permission,
            nameof(entry.Permission));

        RuntimeHostPermission permission =
            entry.Permission switch
            {
                "runtime-host.snapshot.read" =>
                    RuntimeHostPermission.ReadSnapshot,
                "property.cached.read" =>
                    RuntimeHostPermission.ReadCachedProperty,
                "property.authoritative.read" =>
                    RuntimeHostPermission.ReadAuthoritativeProperty,
                "property.write" =>
                    RuntimeHostPermission.WriteProperty,
                "command.execute" =>
                    RuntimeHostPermission.ExecuteCommand,
                "observation.subscribe" =>
                    RuntimeHostPermission.SubscribeObservation,
                "diagnostics.subscribe" =>
                    RuntimeHostPermission.SubscribeDiagnostics,
                "media.capability.read" =>
                    RuntimeHostPermission.ReadMediaCapabilities,
                "media.video.receive" =>
                    RuntimeHostPermission.ReceiveMediaVideo,
                "media.audio.receive" =>
                    RuntimeHostPermission.ReceiveMediaAudio,
                "media.session.start" =>
                    RuntimeHostPermission.StartMediaSession,
                "media.session.negotiate" =>
                    RuntimeHostPermission.NegotiateMediaSession,
                "media.session.stop" =>
                    RuntimeHostPermission.StopMediaSession,
                _ =>
                    throw new ArgumentException(
                        "The authorization grant permission is not supported.",
                        nameof(entry.Permission))
            };

        return new RuntimeHostPermissionGrant(
            entry.PrincipalId,
            permission);
    }

    private sealed class AuthorizationDocument
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion
        {
            get;
            set;
        }

        [JsonPropertyName("grants")]
        public List<AuthorizationGrantEntry?>? Grants
        {
            get;
            set;
        }
    }

    private sealed class AuthorizationGrantEntry
    {
        [JsonPropertyName("principalId")]
        public string? PrincipalId
        {
            get;
            set;
        }

        [JsonPropertyName("permission")]
        public string? Permission
        {
            get;
            set;
        }
    }
}
