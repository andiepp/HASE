using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Creates a new client-enrollment document from an already provisioned
/// public X.509 client certificate without exporting private-key material.
/// </summary>
public static class RuntimeHostClientCredentialEnrollmentProvisioner
{
    /// <summary>
    /// Atomically creates one new enrollment document and never overwrites an
    /// existing target.
    /// </summary>
    public static async Task CreateNewAsync(
        string filePath,
        X509Certificate2 clientCertificate,
        RuntimeHostClientPrincipalId principalId,
        string trustPolicyId,
        CancellationToken cancellationToken = default)
    {
        ValidateFilePath(
            filePath);
        ArgumentNullException.ThrowIfNull(
            clientCertificate);

        if (principalId == default)
        {
            throw new ArgumentException(
                "The client-principal identifier must be specified.",
                nameof(principalId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(
            trustPolicyId,
            nameof(trustPolicyId));

        cancellationToken.ThrowIfCancellationRequested();

        RuntimeHostClientCredentialIdentity credentialIdentity =
            new RuntimeHostX509ClientCredentialIdentityExtractor()
                .Extract(
                    clientCertificate);
        byte[] document =
            Serialize(
                credentialIdentity.CredentialId.Value,
                principalId.Value,
                trustPolicyId);
        string normalizedFilePath =
            Path.GetFullPath(
                filePath);
        string directoryPath =
            Path.GetDirectoryName(
                normalizedFilePath)
            ?? throw new InvalidOperationException(
                "The client-enrollment file path has no parent directory.");

        Directory.CreateDirectory(
            directoryPath);

        string temporaryFilePath =
            Path.Combine(
                directoryPath,
                $".{Path.GetFileName(normalizedFilePath)}."
                + $"{Guid.NewGuid():N}.tmp");
        bool published =
            false;

        try
        {
            await using (var stream =
                new FileStream(
                    temporaryFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    FileOptions.Asynchronous
                    | FileOptions.SequentialScan
                    | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(
                        document,
                        cancellationToken)
                    .ConfigureAwait(
                        false);
                await stream.FlushAsync(
                        cancellationToken)
                    .ConfigureAwait(
                        false);
                stream.Flush(
                    flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();

            File.Move(
                temporaryFilePath,
                normalizedFilePath,
                overwrite: false);
            published =
                true;
        }
        finally
        {
            if (!published)
            {
                try
                {
                    File.Delete(
                        temporaryFilePath);
                }
                catch
                {
                    // Cleanup failure must not hide the provisioning outcome.
                }
            }
        }
    }

    private static void ValidateFilePath(
        string filePath)
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
    }

    private static byte[] Serialize(
        string credentialId,
        string principalId,
        string trustPolicyId)
    {
        using var stream =
            new MemoryStream();
        using (var writer =
            new Utf8JsonWriter(
                stream,
                new JsonWriterOptions
                {
                    Indented =
                        true
                }))
        {
            writer.WriteStartObject();
            writer.WriteNumber(
                "formatVersion",
                1);
            writer.WriteStartArray(
                "enrollments");
            writer.WriteStartObject();
            writer.WriteString(
                "credentialId",
                credentialId);
            writer.WriteString(
                "principalId",
                principalId);
            writer.WriteString(
                "trustPolicyId",
                trustPolicyId);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }
}
