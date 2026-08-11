using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.Python.CredentialProvisioning;

public sealed partial class PythonLaptopMiniPcObservationAuthorizer
{
    private const string Principal = "hase-laptop-python-minipc";
    private static readonly string[] Existing =
    [
        "runtime-host.snapshot.read",
        "property.authoritative.read",
    ];

    [GeneratedRegex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex HashPattern();

    public async Task<PythonObservationAuthorizationResult> AuthorizeAsync(
        PythonObservationAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.AuthorizationPolicyPath)
            || string.IsNullOrWhiteSpace(request.RollbackPath)
            || !Path.IsPathFullyQualified(request.AuthorizationPolicyPath)
            || !Path.IsPathFullyQualified(request.RollbackPath)
            || !HashPattern().IsMatch(request.ExpectedAuthorizationPolicySha256 ?? ""))
            throw Failure("authorization-request-invalid");

        string policy = Path.GetFullPath(request.AuthorizationPolicyPath);
        string rollback = Path.GetFullPath(request.RollbackPath);
        if (PathComparer.Equals(policy, rollback) || !File.Exists(policy)
            || File.Exists(rollback) || Directory.Exists(rollback)
            || Path.GetDirectoryName(rollback) is not string parent
            || !Directory.Exists(parent))
            throw Failure("authorization-target-invalid");

        byte[] original = await File.ReadAllBytesAsync(policy, cancellationToken);
        string transaction = Guid.NewGuid().ToString("N");
        string stage = policy + ".stage-" + transaction;
        bool published = false;
        try
        {
            if (Hash(original) != request.ExpectedAuthorizationPolicySha256)
                throw Failure("authorization-input-revision-mismatch");

            string security = Security(policy);
            byte[] candidate = Candidate(original);
            await File.WriteAllBytesAsync(stage, candidate, cancellationToken);

            if (HashFile(policy) != request.ExpectedAuthorizationPolicySha256)
                throw Failure("authorization-input-revision-mismatch");

            File.Replace(stage, policy, rollback, false);
            published = true;
            string candidateHash = Hash(candidate);

            if (HashFile(policy) != candidateHash
                || HashFile(rollback) != request.ExpectedAuthorizationPolicySha256
                || Security(policy) != security || Security(rollback) != security)
                throw Failure("authorization-publication-invalid");

            return new(transaction, candidateHash, rollback);
        }
        catch (PythonObservationAuthorizationException)
        {
            if (published) Rollback(policy, rollback, request.ExpectedAuthorizationPolicySha256);
            throw;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is SystemException)
        {
            if (published) Rollback(policy, rollback, request.ExpectedAuthorizationPolicySha256);
            throw Failure("authorization-publication-failed");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(original);
            try { File.Delete(stage); } catch (Exception) { }
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
                .Where(x => x.GetProperty("principalId").GetString() == Principal)
                .Select(x => x.GetProperty("permission").GetString()!).ToArray();

            if (permissions.Contains("observation.subscribe", StringComparer.Ordinal))
                throw Failure("observation-already-authorized");
            if (!permissions.SequenceEqual(Existing))
                throw Failure("laptop-minipc-principal-state-invalid");

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("formatVersion", 1);
                writer.WriteStartArray("grants");
                foreach (JsonElement grant in grants) grant.WriteTo(writer);
                writer.WriteStartObject();
                writer.WriteString("principalId", Principal);
                writer.WriteString("permission", "observation.subscribe");
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            byte[] result = stream.ToArray();
            _ = RuntimeHostAuthorizationPolicyFile.Load(result);
            return result;
        }
        catch (PythonObservationAuthorizationException) { throw; }
        catch (Exception) { throw Failure("authorization-policy-invalid"); }
    }

    private static void Rollback(string policy, string rollback, string hash)
    {
        try
        {
            File.Delete(policy);
            File.Move(rollback, policy);
            if (HashFile(policy) != hash) throw new IOException();
        }
        catch (Exception) { throw Failure("authorization-rollback-incomplete"); }
    }

    private static string Security(string path)
    {
        if (!OperatingSystem.IsWindows())
            return Convert.ToInt32(File.GetUnixFileMode(path)).ToString();

        string value = new FileInfo(path).GetAccessControl()
            .GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        int first = value.IndexOf('(');
        if (!value.StartsWith("D:", StringComparison.Ordinal) || first < 0) return value;
        string flags = value[2..first].Replace("AI", "", StringComparison.Ordinal)
            .Replace("AR", "", StringComparison.Ordinal);
        return "D:" + flags + value[first..];
    }

    private static string Hash(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));
    private static string HashFile(string path) => Hash(File.ReadAllBytes(path));
    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static PythonObservationAuthorizationException Failure(string code) => new(code);
}
