using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.Python.CredentialProvisioning;

public sealed partial class PythonLaptopMiniPcPropertyWriteAuthorizer
{
    private const string Principal = "hase-laptop-python-minipc";
    private static readonly string[] Existing =
    [
        "runtime-host.snapshot.read",
        "property.authoritative.read",
        "observation.subscribe",
    ];

    [GeneratedRegex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex HashPattern();

    public async Task<PythonLaptopMiniPcPropertyWriteAuthorizationResult>
        AuthorizeAsync(
            PythonLaptopMiniPcPropertyWriteAuthorizationRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.AuthorizationPolicyPath)
            || string.IsNullOrWhiteSpace(request.RollbackPath)
            || !Path.IsPathFullyQualified(request.AuthorizationPolicyPath)
            || !Path.IsPathFullyQualified(request.RollbackPath)
            || !HashPattern().IsMatch(
                request.ExpectedAuthorizationPolicySha256 ?? ""))
        {
            throw Failure("authorization-request-invalid");
        }

        string policy = Path.GetFullPath(request.AuthorizationPolicyPath);
        string rollback = Path.GetFullPath(request.RollbackPath);
        if (PathComparer.Equals(policy, rollback)
            || !File.Exists(policy)
            || File.Exists(rollback)
            || Directory.Exists(rollback)
            || Path.GetDirectoryName(rollback) is not string parent
            || !Directory.Exists(parent)
            || IsReparsePoint(policy)
            || IsReparsePoint(parent))
        {
            throw Failure("authorization-target-invalid");
        }

        byte[] original = await File.ReadAllBytesAsync(
            policy, cancellationToken).ConfigureAwait(false);
        string transaction = Guid.NewGuid().ToString("N");
        string stage = policy + ".stage-" + transaction;
        bool published = false;
        try
        {
            if (Hash(original) != request.ExpectedAuthorizationPolicySha256)
            {
                throw Failure("authorization-input-revision-mismatch");
            }

            string security = Security(policy);
            byte[] candidate = Candidate(original);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteDurableAsync(
                    stage, candidate, cancellationToken).ConfigureAwait(false);

                if (HashFile(policy)
                    != request.ExpectedAuthorizationPolicySha256)
                {
                    throw Failure("authorization-input-revision-mismatch");
                }

                File.Replace(stage, policy, rollback, false);
                published = true;
                string candidateHash = Hash(candidate);

                if (HashFile(policy) != candidateHash
                    || HashFile(rollback)
                        != request.ExpectedAuthorizationPolicySha256
                    || Security(policy) != security
                    || Security(rollback) != security)
                {
                    throw Failure("authorization-publication-invalid");
                }

                return new(
                    transaction,
                    candidateHash,
                    rollback);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(candidate);
            }
        }
        catch (PythonLaptopMiniPcPropertyWriteAuthorizationException)
        {
            if (published)
            {
                Rollback(
                    policy,
                    rollback,
                    request.ExpectedAuthorizationPolicySha256);
            }
            throw;
        }
        catch (OperationCanceledException)
        {
            if (published)
            {
                Rollback(
                    policy,
                    rollback,
                    request.ExpectedAuthorizationPolicySha256);
            }
            throw;
        }
        catch (Exception exception) when (exception is SystemException)
        {
            if (published)
            {
                Rollback(
                    policy,
                    rollback,
                    request.ExpectedAuthorizationPolicySha256);
            }
            throw Failure("authorization-publication-failed");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(original);
            TryDelete(stage);
        }
    }

    private static byte[] Candidate(byte[] original)
    {
        try
        {
            _ = RuntimeHostAuthorizationPolicyFile.Load(original);
            using JsonDocument source = JsonDocument.Parse(original);
            JsonElement[] grants = source.RootElement.GetProperty("grants")
                .EnumerateArray().ToArray();
            string[] permissions = grants
                .Where(item =>
                    item.GetProperty("principalId").GetString() == Principal)
                .Select(item =>
                    item.GetProperty("permission").GetString()!)
                .ToArray();

            if (permissions.Contains(
                    "property.write", StringComparer.Ordinal))
            {
                throw Failure("property-write-already-authorized");
            }
            if (!permissions.SequenceEqual(Existing))
            {
                throw Failure("laptop-minipc-principal-state-invalid");
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(
                stream,
                new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("formatVersion", 1);
                writer.WriteStartArray("grants");
                foreach (JsonElement grant in grants)
                {
                    grant.WriteTo(writer);
                }
                writer.WriteStartObject();
                writer.WriteString("principalId", Principal);
                writer.WriteString("permission", "property.write");
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            byte[] result = stream.ToArray();
            _ = RuntimeHostAuthorizationPolicyFile.Load(result);
            return result;
        }
        catch (PythonLaptopMiniPcPropertyWriteAuthorizationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException
                or InvalidDataException
                or KeyNotFoundException
                or InvalidOperationException)
        {
            throw Failure("authorization-policy-invalid");
        }
    }

    private static async Task WriteDurableAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(
            content, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(true);
    }

    private static void Rollback(
        string policy,
        string rollback,
        string expectedHash)
    {
        try
        {
            File.Delete(policy);
            File.Move(rollback, policy);
            if (HashFile(policy) != expectedHash)
            {
                throw new IOException();
            }
        }
        catch (Exception)
        {
            throw Failure("authorization-rollback-incomplete");
        }
    }

    private static string Security(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Convert.ToInt32(File.GetUnixFileMode(path)).ToString();
        }

        string value = new FileInfo(path).GetAccessControl()
            .GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        int first = value.IndexOf('(');
        if (!value.StartsWith("D:", StringComparison.Ordinal) || first < 0)
        {
            return value;
        }
        string flags = value[2..first]
            .Replace("AI", "", StringComparison.Ordinal)
            .Replace("AR", "", StringComparison.Ordinal);
        return "D:" + flags + value[first..];
    }

    private static string Hash(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
        }
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static PythonLaptopMiniPcPropertyWriteAuthorizationException
        Failure(string code) => new(code);
}
