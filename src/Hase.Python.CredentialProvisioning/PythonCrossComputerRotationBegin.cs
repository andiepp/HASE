using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.Python.CredentialProvisioning;

/// <summary>Publishes only the Runtime Host half of a cross-computer rotation.</summary>
public sealed class PythonCrossComputerRotationBegin
{
    private const string JournalName = "cross-computer-rotation.transaction.json";

    public async Task<PythonCrossComputerRotationBeginResult> BeginAsync(
        PythonCrossComputerRotationBeginRequest request,
        X509Certificate2 signingRoot,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(signingRoot);
        Validated input = Validate(request);
        string journalPath = Path.Combine(input.Directory, JournalName);
        if (File.Exists(journalPath)) Fail("cross-rotation-recovery-required");

        byte[]? requestBytes = null;
        byte[]? profileBytes = null;
        byte[]? enrollmentBytes = null;
        byte[]? policyBytes = null;
        byte[]? overlapBytes = null;
        byte[]? finalBytes = null;
        using PythonClientCredentialMaterial replacement =
            PythonClientCredentialFactory.Create(signingRoot, utcNow, input.Validity);
        string id = Guid.NewGuid().ToString("N");
        string backupPath = input.EnrollmentPath + ".backup-" + id;
        string stagePath = input.EnrollmentPath + ".stage-" + id;
        string finalPath = input.EnrollmentPath + ".final-stage-" + id;
        string security = CaptureSecurity(input.EnrollmentPath);
        try
        {
            requestBytes = await File.ReadAllBytesAsync(input.RequestPath,
                cancellationToken).ConfigureAwait(false);
            profileBytes = await File.ReadAllBytesAsync(input.ProfilePath,
                cancellationToken).ConfigureAwait(false);
            enrollmentBytes = await File.ReadAllBytesAsync(input.EnrollmentPath,
                cancellationToken).ConfigureAwait(false);
            policyBytes = await File.ReadAllBytesAsync(input.PolicyPath,
                cancellationToken).ConfigureAwait(false);
            Evidence evidence = LoadEvidence(requestBytes);
            ValidateEvidence(evidence, profileBytes, enrollmentBytes, policyBytes,
                input, replacement.CredentialId, utcNow);
            (overlapBytes, finalBytes) = CreateEnrollments(enrollmentBytes,
                evidence.ExpectedCurrentCredentialId, replacement.CredentialId,
                evidence.PrincipalId, input.TrustPolicyId, utcNow);

            await CreateArchiveAsync(input.ArchivePath, replacement, profileBytes,
                evidence, id, cancellationToken).ConfigureAwait(false);
            string archiveHash = HashFile(input.ArchivePath);
            var journal = new Journal(id, "prepared", input.EnrollmentPath,
                input.EnrollmentHash, Hash(overlapBytes), Hash(finalBytes),
                input.PolicyPath, input.PolicyHash, evidence.ExpectedCurrentCredentialId,
                replacement.CredentialId, backupPath, stagePath, finalPath,
                input.ArchivePath, archiveHash, security);
            await WriteNewAsync(journalPath, JsonSerializer.SerializeToUtf8Bytes(
                journal, JsonOptions()), cancellationToken).ConfigureAwait(false);
            await WriteNewAsync(stagePath, overlapBytes, cancellationToken)
                .ConfigureAwait(false);
            await WriteNewAsync(finalPath, finalBytes, cancellationToken)
                .ConfigureAwait(false);
            RestoreSecurity(stagePath, security);
            RestoreSecurity(finalPath, security);
            File.Move(input.EnrollmentPath, backupPath);
            File.Move(stagePath, input.EnrollmentPath);
            RestoreSecurity(input.EnrollmentPath, security);
            journal = journal with { Phase = "overlap-published" };
            WriteReplace(journalPath, JsonSerializer.SerializeToUtf8Bytes(
                journal, JsonOptions()));
            return new(id, "OverlapPublished", true, true);
        }
        catch (PythonCrossComputerRotationException) { TryRollback(input,
            journalPath, backupPath, stagePath, finalPath); throw; }
        catch (OperationCanceledException) { TryRollback(input, journalPath,
            backupPath, stagePath, finalPath); throw; }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or CryptographicException
            or JsonException or InvalidDataException or ArgumentException
            or InvalidOperationException or FormatException)
        {
            TryRollback(input, journalPath, backupPath, stagePath, finalPath);
            throw new PythonCrossComputerRotationException(
                "cross-rotation-begin-failed");
        }
        finally
        {
            Zero(requestBytes); Zero(profileBytes); Zero(enrollmentBytes);
            Zero(policyBytes); Zero(overlapBytes); Zero(finalBytes);
        }
    }

    private static void ValidateEvidence(Evidence evidence, byte[] profile,
        byte[] enrollment, byte[] policy, Validated input,
        string replacementId, DateTimeOffset utcNow)
    {
        string[] grants = ["command.execute", "observation.subscribe",
            "property.authoritative.read", "property.write",
            "runtime-host.snapshot.read"];
        if (evidence.SchemaVersion != 1
            || evidence.Purpose != "hase-laptop-minipc-python-cross-computer-rotation-request"
            || evidence.TargetId != "minipc-runtime-host"
            || evidence.PrincipalId != "hase-laptop-python-minipc"
            || !evidence.ExpectedGrants.Order(StringComparer.Ordinal)
                .SequenceEqual(grants)
            || evidence.ExpectedCurrentCredentialId == replacementId
            || evidence.ProfileSha256 != Hash(profile)
            || !IsHash(evidence.CertificateSha256)
            || !IsHash(evidence.PrivateKeySha256)
            || !IsHash(evidence.TrustedServerCertificateSha256)
            || Hash(enrollment) != input.EnrollmentHash
            || Hash(policy) != input.PolicyHash)
            Fail("cross-rotation-request-mismatch");
        _ = PythonRuntimeHostProfileDocument.Load(profile);
        RuntimeHostAuthorizationPolicy authorization =
            RuntimeHostAuthorizationPolicyFile.Load(policy);
        foreach (string grant in grants)
            if (!authorization.IsGranted(evidence.PrincipalId,
                    new RuntimeHostPermission(grant)))
                Fail("cross-rotation-authorization-mismatch");
        using (JsonDocument policyDocument = JsonDocument.Parse(policy))
        {
            string[] actual = policyDocument.RootElement.GetProperty("grants")
                .EnumerateArray()
                .Where(value => value.GetProperty("principalId").GetString()
                    == evidence.PrincipalId)
                .Select(value => value.GetProperty("permission").GetString()!)
                .Order(StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(grants))
                Fail("cross-rotation-authorization-mismatch");
        }
        RuntimeHostClientCredentialEnrollmentRegistry registry =
            RuntimeHostClientCredentialEnrollmentRegistryFile.Load(enrollment);
        if (!Resolve(registry, evidence.ExpectedCurrentCredentialId, utcNow,
                evidence.PrincipalId, input.TrustPolicyId))
            Fail("cross-rotation-enrollment-mismatch");
    }

    private static (byte[], byte[]) CreateEnrollments(byte[] source,
        string oldId, string newId, string principal, string trust,
        DateTimeOffset utcNow)
    {
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement list = document.RootElement.GetProperty("enrollments");
        var entries = list.EnumerateArray().Select(value => new Enrollment(
            value.GetProperty("credentialId").GetString()!,
            value.GetProperty("principalId").GetString()!,
            value.GetProperty("trustPolicyId").GetString()!)).ToList();
        if (entries.Count(value => value.CredentialId == oldId
                && value.PrincipalId == principal && value.TrustPolicyId == trust) != 1
            || entries.Any(value => value.CredentialId == newId))
            Fail("cross-rotation-enrollment-mismatch");
        var replacement = new Enrollment(newId, principal, trust);
        byte[] overlap = Serialize(entries.Append(replacement));
        byte[] final = Serialize(entries.Where(value => value.CredentialId != oldId)
            .Append(replacement));
        RuntimeHostClientCredentialEnrollmentRegistry overlapRegistry =
            RuntimeHostClientCredentialEnrollmentRegistryFile.Load(overlap);
        RuntimeHostClientCredentialEnrollmentRegistry finalRegistry =
            RuntimeHostClientCredentialEnrollmentRegistryFile.Load(final);
        if (!Resolve(overlapRegistry, oldId, utcNow, principal, trust)
            || !Resolve(overlapRegistry, newId, utcNow, principal, trust)
            || Resolve(finalRegistry, oldId, utcNow, principal, trust)
            || !Resolve(finalRegistry, newId, utcNow, principal, trust))
            Fail("cross-rotation-enrollment-mismatch");
        return (overlap, final);
    }

    private static async Task CreateArchiveAsync(string path,
        PythonClientCredentialMaterial replacement, byte[] profile,
        Evidence evidence, string transactionId, CancellationToken token)
    {
        byte[] certificate = replacement.CertificatePem.ToArray();
        byte[] key = replacement.PrivateKeyPem.ToArray();
        try
        {
            byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                purpose = "hase-laptop-minipc-python-cross-computer-rotation-package",
                transactionId,
                principalId = evidence.PrincipalId,
                currentCredentialId = evidence.ExpectedCurrentCredentialId,
                replacementCredentialId = replacement.CredentialId,
                files = new[]
                {
                    new { name = "client-certificate.pem", sha256 = Hash(certificate) },
                    new { name = "private-key.pem", sha256 = Hash(key) },
                    new { name = "runtime-host-profile.json", sha256 = Hash(profile) },
                }
            }, JsonOptions());
            try
            {
                await using FileStream stream = new(path, FileMode.CreateNew,
                    FileAccess.ReadWrite, FileShare.None, 4096,
                    FileOptions.Asynchronous | FileOptions.WriteThrough);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create,
                    leaveOpen: true);
                await AddAsync(archive, "client-certificate.pem", certificate, token);
                await AddAsync(archive, "private-key.pem", key, token);
                await AddAsync(archive, "runtime-host-profile.json", profile, token);
                await AddAsync(archive, "transfer-manifest.json", manifest, token);
                archive.Dispose(); stream.Flush(flushToDisk: true);
            }
            finally { Zero(manifest); }
        }
        finally { Zero(certificate); Zero(key); }
    }

    private static async Task AddAsync(ZipArchive archive, string name,
        byte[] bytes, CancellationToken token)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name,
            CompressionLevel.Optimal);
        await using Stream output = entry.Open();
        await output.WriteAsync(bytes, token).ConfigureAwait(false);
    }

    private static Evidence LoadEvidence(byte[] bytes)
    {
        using JsonDocument parsed = JsonDocument.Parse(bytes);
        RejectDuplicates(parsed.RootElement);
        Evidence? evidence = JsonSerializer.Deserialize<Evidence>(bytes,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow });
        return evidence ?? throw new InvalidDataException();
    }

    private static byte[] Serialize(IEnumerable<Enrollment> entries) =>
        JsonSerializer.SerializeToUtf8Bytes(new { formatVersion = 1,
            enrollments = entries.Select(value => new { credentialId = value.CredentialId,
                principalId = value.PrincipalId, trustPolicyId = value.TrustPolicyId }) },
            JsonOptions());

    private static bool Resolve(RuntimeHostClientCredentialEnrollmentRegistry registry,
        string id, DateTimeOffset now, string principal, string trust)
    {
        var identity = new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(id));
        return registry.TryResolve(identity, now, out RuntimeHostClientPrincipal? found)
            && found!.PrincipalId == principal && found.TrustPolicyId == trust;
    }

    private static Validated Validate(PythonCrossComputerRotationBeginRequest value)
    {
        string request = FilePath(value.RotationRequestPath);
        string profile = FilePath(value.ProfileTemplatePath);
        string enrollment = FilePath(value.EnrollmentPath);
        string policy = FilePath(value.AuthorizationPolicyPath);
        string directory = DirectoryPath(value.ProvisioningDirectory);
        string archive = NewPath(value.TransferArchivePath);
        if (Path.GetFullPath(Path.GetDirectoryName(archive)!) != directory
            || value.Validity <= TimeSpan.Zero
            || value.Validity > PythonClientCredentialFactory.MaximumValidity)
            Fail("cross-rotation-input-invalid");
        HashValue(value.ExpectedEnrollmentSha256);
        HashValue(value.ExpectedAuthorizationPolicySha256);
        return new(request, profile, enrollment, policy, directory, archive,
            value.TrustPolicyId, value.Validity, value.ExpectedEnrollmentSha256,
            value.ExpectedAuthorizationPolicySha256);
    }

    private static string FilePath(string value)
    { string path = Absolute(value); if (!File.Exists(path) || Reparse(path))
        Fail("cross-rotation-input-invalid"); return path; }
    private static string DirectoryPath(string value)
    { string path = Absolute(value); if (!Directory.Exists(path) || Reparse(path))
        Fail("cross-rotation-input-invalid"); return path; }
    private static string NewPath(string value)
    { string path = Absolute(value); if (File.Exists(path) || Directory.Exists(path))
        Fail("cross-rotation-output-exists"); return path; }
    private static string Absolute(string value)
    { if (string.IsNullOrWhiteSpace(value) || value != value.Trim()
        || !Path.IsPathFullyQualified(value)) Fail("cross-rotation-input-invalid");
      return Path.GetFullPath(value); }
    private static void HashValue(string value)
    { if (value.Length != 64 || value.Any(c => c is not (>= '0' and <= '9'
        or >= 'a' and <= 'f'))) Fail("cross-rotation-hash-invalid"); }

    private static async Task WriteNewAsync(string path, byte[] bytes,
        CancellationToken token)
    { try { await using FileStream stream = new(path, FileMode.CreateNew,
        FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous
        | FileOptions.WriteThrough); await stream.WriteAsync(bytes, token);
        await stream.FlushAsync(token); stream.Flush(true); }
      finally { Zero(bytes); } }
    private static void WriteReplace(string path, byte[] bytes)
    { try { string next = path + ".next"; File.WriteAllBytes(next, bytes);
        File.Move(next, path, true); } finally { Zero(bytes); } }
    private static void TryRollback(Validated input, string journal,
        string backup, string stage, string final)
    { try { if (File.Exists(backup)) { File.Delete(input.EnrollmentPath);
        File.Move(backup, input.EnrollmentPath); }
        File.Delete(stage); File.Delete(final); File.Delete(journal + ".next");
        File.Delete(journal); File.Delete(input.ArchivePath); } catch { } }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(
        SHA256.HashData(bytes));
    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        try { return Convert.ToHexStringLower(hash); }
        finally { Zero(hash); }
    }
    private static bool IsHash(string value) => value.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string CaptureSecurity(string path) => OperatingSystem.IsWindows()
        ? new FileInfo(path).GetAccessControl().GetSecurityDescriptorSddlForm(
            AccessControlSections.Access) : ((int)File.GetUnixFileMode(path)).ToString();
    private static void RestoreSecurity(string path, string security)
    { if (OperatingSystem.IsWindows()) { var descriptor = new FileSecurity();
        descriptor.SetSecurityDescriptorSddlForm(security,
            AccessControlSections.Access); new FileInfo(path).SetAccessControl(descriptor); }
      else File.SetUnixFileMode(path, (UnixFileMode)int.Parse(security)); }
    private static bool Reparse(string path) => (File.GetAttributes(path)
        & FileAttributes.ReparsePoint) != 0;
    private static void RejectDuplicates(JsonElement element)
    { if (element.ValueKind == JsonValueKind.Object) { var names = new HashSet<string>();
        foreach (JsonProperty property in element.EnumerateObject()) { if (!names.Add(
            property.Name)) throw new InvalidDataException(); RejectDuplicates(property.Value); } }
      else if (element.ValueKind == JsonValueKind.Array) foreach (JsonElement item
        in element.EnumerateArray()) RejectDuplicates(item); }
    private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true };
    private static void Zero(byte[]? bytes) { if (bytes is not null)
        CryptographicOperations.ZeroMemory(bytes); }
    [DoesNotReturn] private static void Fail(string code) =>
        throw new PythonCrossComputerRotationException(code);

    private sealed record Validated(string RequestPath, string ProfilePath,
        string EnrollmentPath, string PolicyPath, string Directory,
        string ArchivePath, string TrustPolicyId, TimeSpan Validity,
        string EnrollmentHash, string PolicyHash);
    private sealed record Enrollment(string CredentialId, string PrincipalId,
        string TrustPolicyId);
    private sealed record Evidence(int SchemaVersion, string Purpose,
        string RepositoryHead, string TargetId, string PrincipalId,
        string ExpectedCurrentCredentialId, IReadOnlyList<string> ExpectedGrants,
        string ProfileSha256, string CertificateSha256, string PrivateKeySha256,
        string TrustedServerCertificateSha256, string CreatedUtc);
    private sealed record Journal(string TransactionId, string Phase,
        string EnrollmentPath, string EnrollmentSha256, string OverlapSha256,
        string FinalSha256, string AuthorizationPolicyPath,
        string AuthorizationPolicySha256, string CurrentCredentialId,
        string ReplacementCredentialId, string EnrollmentBackupPath,
        string EnrollmentStagePath, string FinalEnrollmentPath,
        string TransferArchivePath, string TransferArchiveSha256,
        string EnrollmentSecurity);
}
