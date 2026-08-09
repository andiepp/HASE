using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.Python.CredentialProvisioning;

public sealed partial class PythonPropertyWriteAuthorizer
{
    private const string PrincipalId = "hase-python-automation";
    private static readonly string[] ExistingPermissions =
    ["runtime-host.snapshot.read", "property.authoritative.read"];
    private static readonly HashSet<string> ProfileProperties = new(
        ["formatVersion", "identityFilePath",
         "privateNetworkConfigurationFilePath", "endpointCompositionFilePath",
         "maximumDiagnosticLevel", "includeByteBufferSimulation",
         "remoteDiagnosticsEnabled", "remoteDiagnosticsMaximumLevel"],
        StringComparer.Ordinal);

    [GeneratedRegex("\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    public async Task<PythonPropertyWriteAuthorizationResult> AuthorizeAsync(
        PythonPropertyWriteAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatedRequest validated = Validate(request);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] policyOriginal = await ReadAsync(
            validated.PolicyPath, cancellationToken).ConfigureAwait(false);
        byte[] profileOriginal = await ReadAsync(
            validated.ProfilePath, cancellationToken).ConfigureAwait(false);
        string transactionId = Guid.NewGuid().ToString("N");
        string policyStage = validated.PolicyPath + ".stage-" + transactionId;
        string profileStage = validated.ProfilePath + ".stage-" + transactionId;
        bool policyPublished = false;
        bool profilePublished = false;
        try
        {
            RequireHash(policyOriginal, validated.ExpectedPolicySha256);
            RequireHash(profileOriginal, validated.ExpectedProfileSha256);
            string policySecurity = CaptureSecurity(validated.PolicyPath);
            string profileSecurity = CaptureSecurity(validated.ProfilePath);
            byte[] policyCandidate = CreatePolicyCandidate(policyOriginal);
            byte[] profileCandidate = CreateProfileCandidate(
                profileOriginal, validated.PolicyPath);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteDurableAsync(policyStage, policyCandidate,
                    cancellationToken).ConfigureAwait(false);
                await WriteDurableAsync(profileStage, profileCandidate,
                    cancellationToken).ConfigureAwait(false);
                RequireCurrentRevisions(validated);

                File.Replace(policyStage, validated.PolicyPath,
                    validated.PolicyRollbackPath, false);
                policyPublished = true;
                File.Replace(profileStage, validated.ProfilePath,
                    validated.ProfileRollbackPath, false);
                profilePublished = true;

                string policyHash = Hash(policyCandidate);
                string profileHash = Hash(profileCandidate);
                if (!ValidPublication(validated, policyHash, profileHash,
                    policySecurity, profileSecurity))
                {
                    throw Failure("authorization-publication-invalid");
                }
                return new PythonPropertyWriteAuthorizationResult(
                    transactionId, policyHash, profileHash,
                    validated.PolicyRollbackPath,
                    validated.ProfileRollbackPath);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(policyCandidate);
                CryptographicOperations.ZeroMemory(profileCandidate);
            }
        }
        catch (PythonPropertyWriteAuthorizationException)
        {
            RollBack(validated, policyPublished, profilePublished);
            throw;
        }
        catch (OperationCanceledException)
        {
            RollBack(validated, policyPublished, profilePublished);
            throw;
        }
        catch (Exception exception) when (exception is SystemException)
        {
            RollBack(validated, policyPublished, profilePublished);
            throw Failure("authorization-publication-failed");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(policyOriginal);
            CryptographicOperations.ZeroMemory(profileOriginal);
            TryDelete(policyStage);
            TryDelete(profileStage);
        }
    }

    private static ValidatedRequest Validate(
        PythonPropertyWriteAuthorizationRequest request)
    {
        string[] supplied = [request.AuthorizationPolicyPath,
            request.ApplicationProfilePath, request.PolicyRollbackPath,
            request.ProfileRollbackPath];
        if (supplied.Any(string.IsNullOrWhiteSpace)
            || supplied.Any(path => !Path.IsPathFullyQualified(path))
            || !Sha256Pattern().IsMatch(
                request.ExpectedAuthorizationPolicySha256 ?? string.Empty)
            || !Sha256Pattern().IsMatch(
                request.ExpectedApplicationProfileSha256 ?? string.Empty))
        {
            throw Failure("authorization-request-invalid");
        }
        string[] paths = supplied.Select(Path.GetFullPath).ToArray();
        if (paths.Distinct(PathComparer).Count() != paths.Length
            || !File.Exists(paths[0]) || !File.Exists(paths[1])
            || File.Exists(paths[2]) || File.Exists(paths[3])
            || Directory.Exists(paths[2]) || Directory.Exists(paths[3])
            || paths.Any(path => Path.GetDirectoryName(path) is not string parent
                || !Directory.Exists(parent) || IsReparsePoint(parent))
            || IsReparsePoint(paths[0]) || IsReparsePoint(paths[1]))
        {
            throw Failure("authorization-target-invalid");
        }
        return new ValidatedRequest(paths[0],
            request.ExpectedAuthorizationPolicySha256, paths[1],
            request.ExpectedApplicationProfileSha256, paths[2], paths[3]);
    }

    private static byte[] CreatePolicyCandidate(byte[] original)
    {
        try
        {
            _ = RuntimeHostAuthorizationPolicyFile.Load(original);
            using JsonDocument source = JsonDocument.Parse(original);
            JsonElement[] grants = source.RootElement.GetProperty("grants")
                .EnumerateArray().ToArray();
            string[] permissions = grants
                .Where(grant => grant.GetProperty("principalId").GetString()
                    == PrincipalId)
                .Select(grant => grant.GetProperty("permission").GetString()!)
                .ToArray();
            if (permissions.Contains("property.write", StringComparer.Ordinal))
                throw Failure("property-write-already-authorized");
            if (!permissions.SequenceEqual(ExistingPermissions))
                throw Failure("python-principal-state-invalid");
            using var stream = new MemoryStream();
            using (var writer = Writer(stream))
            {
                writer.WriteStartObject();
                writer.WriteNumber("formatVersion", 1);
                writer.WriteStartArray("grants");
                foreach (JsonElement grant in grants) grant.WriteTo(writer);
                writer.WriteStartObject();
                writer.WriteString("principalId", PrincipalId);
                writer.WriteString("permission", "property.write");
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            byte[] candidate = stream.ToArray();
            _ = RuntimeHostAuthorizationPolicyFile.Load(candidate);
            return candidate;
        }
        catch (PythonPropertyWriteAuthorizationException) { throw; }
        catch (Exception exception) when (exception is JsonException
            or InvalidDataException or KeyNotFoundException
            or InvalidOperationException)
        {
            throw Failure("authorization-policy-invalid");
        }
    }

    private static byte[] CreateProfileCandidate(byte[] original, string policy)
    {
        try
        {
            ReadOnlyMemory<byte> profileJson = original;
            if (original.Length >= 3
                && original[0] == 0xef
                && original[1] == 0xbb
                && original[2] == 0xbf)
                profileJson = original.AsMemory(3);
            using JsonDocument source = JsonDocument.Parse(profileJson);
            JsonElement root = source.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
            JsonProperty[] properties = root.EnumerateObject().ToArray();
            string[] names = properties.Select(item => item.Name).ToArray();
            string[] required = ["formatVersion", "identityFilePath",
                "privateNetworkConfigurationFilePath",
                "endpointCompositionFilePath", "maximumDiagnosticLevel",
                "includeByteBufferSimulation"];
            if (names.Distinct(StringComparer.Ordinal).Count() != names.Length
                || names.Any(name => !ProfileProperties.Contains(name))
                || required.Any(name => !names.Contains(name, StringComparer.Ordinal))
                || names.Contains("authorizationPolicyFilePath",
                    StringComparer.Ordinal)
                || root.GetProperty("formatVersion").GetInt32() != 1
                || (root.TryGetProperty("remoteDiagnosticsEnabled", out var enabled)
                    && enabled.ValueKind != JsonValueKind.False))
            {
                throw Failure("application-profile-state-invalid");
            }
            using var stream = new MemoryStream();
            using (var writer = Writer(stream))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in properties)
                    property.WriteTo(writer);
                writer.WriteString("authorizationPolicyFilePath", policy);
                writer.WriteEndObject();
            }
            return stream.ToArray();
        }
        catch (PythonPropertyWriteAuthorizationException) { throw; }
        catch (Exception exception) when (exception is JsonException
            or InvalidDataException or KeyNotFoundException
            or InvalidOperationException)
        {
            throw Failure("application-profile-invalid");
        }
    }

    private static Utf8JsonWriter Writer(Stream stream) => new(stream,
        new JsonWriterOptions { Indented = true });

    private static async Task<byte[]> ReadAsync(string path,
        CancellationToken token)
    {
        try { return await File.ReadAllBytesAsync(path, token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        { throw Failure("authorization-input-unavailable"); }
    }

    private static async Task WriteDurableAsync(string path, byte[] content,
        CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(content, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
        stream.Flush(true);
    }

    private static void RequireHash(byte[] content, string expected)
    {
        if (!string.Equals(Hash(content), expected, StringComparison.Ordinal))
            throw Failure("authorization-input-revision-mismatch");
    }

    private static void RequireCurrentRevisions(ValidatedRequest request)
    {
        if (!string.Equals(HashFile(request.PolicyPath),
                request.ExpectedPolicySha256, StringComparison.Ordinal)
            || !string.Equals(HashFile(request.ProfilePath),
                request.ExpectedProfileSha256, StringComparison.Ordinal))
            throw Failure("authorization-input-revision-mismatch");
    }

    private static bool ValidPublication(ValidatedRequest value,
        string policyHash, string profileHash, string policySecurity,
        string profileSecurity) =>
        string.Equals(HashFile(value.PolicyPath), policyHash,
            StringComparison.Ordinal)
        && string.Equals(HashFile(value.ProfilePath), profileHash,
            StringComparison.Ordinal)
        && string.Equals(HashFile(value.PolicyRollbackPath),
            value.ExpectedPolicySha256, StringComparison.Ordinal)
        && string.Equals(HashFile(value.ProfileRollbackPath),
            value.ExpectedProfileSha256, StringComparison.Ordinal)
        && CaptureSecurity(value.PolicyPath) == policySecurity
        && CaptureSecurity(value.PolicyRollbackPath) == policySecurity
        && CaptureSecurity(value.ProfilePath) == profileSecurity
        && CaptureSecurity(value.ProfileRollbackPath) == profileSecurity;

    private static void RollBack(ValidatedRequest value, bool policy, bool profile)
    {
        if (!policy && !profile) return;
        try
        {
            if (profile)
            {
                File.Delete(value.ProfilePath);
                File.Move(value.ProfileRollbackPath, value.ProfilePath);
            }
            if (policy)
            {
                File.Delete(value.PolicyPath);
                File.Move(value.PolicyRollbackPath, value.PolicyPath);
            }
            RequireCurrentRevisions(value);
        }
        catch (Exception)
        {
            throw Failure("authorization-rollback-incomplete");
        }
    }

    private static string CaptureSecurity(string path) =>
        OperatingSystem.IsWindows()
            ? NormalizeAccessDescriptor(new FileInfo(path).GetAccessControl()
                .GetSecurityDescriptorSddlForm(AccessControlSections.Access))
            : Convert.ToInt32(File.GetUnixFileMode(path)).ToString();

    private static string NormalizeAccessDescriptor(string value)
    {
        int firstAce = value.IndexOf('(');
        if (!value.StartsWith("D:", StringComparison.Ordinal) || firstAce < 0)
            return value;
        string flags = value[2..firstAce]
            .Replace("AI", string.Empty, StringComparison.Ordinal)
            .Replace("AR", string.Empty, StringComparison.Ordinal);
        return "D:" + flags + value[firstAce..];
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
    { try { File.Delete(path); } catch (Exception) { } }
    private static PythonPropertyWriteAuthorizationException Failure(string code)
        => new(code);
    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed record ValidatedRequest(string PolicyPath,
        string ExpectedPolicySha256, string ProfilePath,
        string ExpectedProfileSha256, string PolicyRollbackPath,
        string ProfileRollbackPath);
}
