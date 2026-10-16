using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.Python.CredentialProvisioning;

/// <summary>
/// Publishes the re-issue of an expired credential (ADR-0083) as one durable
/// transition without overlap: the replacement certificate, its key, the
/// unchanged profile and the final enrollment, in which the replacement
/// takes the expired credential's place. The exact old files are retained
/// beside their targets until an explicit recovery cleans them up.
/// </summary>
public sealed class PythonCredentialReissuePublisher
{
    private const string JournalName =
        "python-credential-reissue.transaction.json";
    private const string RotationJournalName =
        "python-credential-rotation.transaction.json";
    private readonly Action<PythonCredentialReissuePublicationStep>?
        stepReached;

    public PythonCredentialReissuePublisher()
    {
    }

    internal PythonCredentialReissuePublisher(
        Action<PythonCredentialReissuePublicationStep> stepReached)
    {
        this.stepReached = stepReached;
    }

    public async Task<PythonCredentialRotationPublicationResult> BeginAsync(
        PythonCredentialRotationPublicationRequest request,
        PythonCredentialRotationCandidates candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidates);
        ValidatedRequest validated = Validate(request);
        string journalPath = Path.Combine(validated.Directory, JournalName);
        if (File.Exists(journalPath)) Fail("reissue-recovery-required");
        if (File.Exists(Path.Combine(validated.Directory, RotationJournalName)))
            Fail("rotation-recovery-required");

        await ValidateSourcesAsync(validated, cancellationToken)
            .ConfigureAwait(false);
        ValidateCandidates(validated, candidates);
        string id = Guid.NewGuid().ToString("N");
        Entry[] entries =
        [
            CreateEntry(validated.CertificatePath,
                candidates.ReplacementCertificatePem.Span, id),
            CreateEntry(validated.PrivateKeyPath,
                candidates.ReplacementPrivateKeyPem.Span, id),
            CreateEntry(validated.ProfilePath,
                candidates.ProfileDocument.Span, id),
            CreateEntry(validated.EnrollmentPath,
                candidates.FinalEnrollmentDocument.Span, id),
        ];
        var journal = new Journal(
            id, "created", validated.PolicyPath, validated.PolicyHash,
            candidates.CurrentCredentialId,
            candidates.ReplacementCredentialId,
            entries);

        try
        {
            await WriteJournalAsync(journalPath, journal, cancellationToken)
                .ConfigureAwait(false);
            await StageAsync(entries, candidates, cancellationToken)
                .ConfigureAwait(false);
            journal = journal with { Phase = "staged" };
            await WriteJournalAsync(journalPath, journal, cancellationToken)
                .ConfigureAwait(false);
            Reach(PythonCredentialReissuePublicationStep.Staged);

            for (int index = 0; index < entries.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Entry entry = entries[index];
                File.Move(entry.TargetPath, entry.BackupPath);
                File.Move(entry.StagePath, entry.TargetPath);
                RestoreSecurity(entry.TargetPath, entry.Security);
                journal = journal with { Phase = $"published-{index + 1}" };
                await WriteJournalAsync(journalPath, journal, cancellationToken)
                    .ConfigureAwait(false);
                Reach((PythonCredentialReissuePublicationStep)(index + 2));
            }

            ValidatePublished(journal);
            ValidateFinalEnrollment(journal, validated.EnrollmentPath);
            EnsureHash(validated.PolicyPath, validated.PolicyHash,
                "authorization-policy-revision-mismatch");
            journal = journal with { Phase = "committed" };
            await WriteJournalAsync(journalPath, journal, cancellationToken)
                .ConfigureAwait(false);
            Reach(PythonCredentialReissuePublicationStep.Committed);
            return new(id, "Reissued", RollbackRetained: true);
        }
        catch (OperationCanceledException)
        {
            TryRollBack(validated);
            throw;
        }
        catch (PythonCredentialRotationPublicationException)
        {
            TryRollBack(validated);
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or CryptographicException
            or JsonException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException)
        {
            TryRollBack(validated);
            throw new PythonCredentialRotationPublicationException(
                "reissue-publication-failed");
        }
    }

    /// <summary>
    /// Rolls an interrupted re-issue back to the exact expired sources, or
    /// removes the retained old files of a committed one. The request names
    /// the sources' hashes from before the re-issue.
    /// </summary>
    public PythonCredentialRotationPublicationResult Recover(
        PythonCredentialRotationPublicationRequest request)
    {
        ValidatedRequest validated = Validate(request);
        string journalPath = Path.Combine(validated.Directory, JournalName);
        Journal journal = ReadJournal(journalPath, validated);
        if (journal.Phase == "committed")
        {
            ValidatePublished(journal);
            ValidateFinalEnrollment(journal, validated.EnrollmentPath);
            Cleanup(journal, journalPath);
            return new(journal.TransactionId, "CommittedCleanup",
                RollbackRetained: false);
        }

        ValidateRecoverable(journal);
        RollBack(journal, journalPath);
        return new(journal.TransactionId, "RolledBack",
            RollbackRetained: false);
    }

    private static async Task StageAsync(
        Entry[] entries,
        PythonCredentialRotationCandidates candidates,
        CancellationToken token)
    {
        ReadOnlyMemory<byte>[] values =
        [
            candidates.ReplacementCertificatePem,
            candidates.ReplacementPrivateKeyPem,
            candidates.ProfileDocument,
            candidates.FinalEnrollmentDocument,
        ];
        for (int index = 0; index < entries.Length; index++)
        {
            await WriteNewAsync(entries[index].StagePath, values[index], token)
                .ConfigureAwait(false);
            RestoreSecurity(entries[index].StagePath, entries[index].Security);
            EnsureHash(entries[index].StagePath, entries[index].CandidateSha256,
                "staged-candidate-invalid");
        }
    }

    private static async Task WriteNewAsync(
        string path,
        ReadOnlyMemory<byte> value,
        CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(value, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static void ValidateCandidates(
        ValidatedRequest request,
        PythonCredentialRotationCandidates candidates)
    {
        if (Hash(candidates.ProfileDocument.Span) != request.ProfileHash
            || Hash(candidates.AuthorizationPolicyDocument.Span)
                != request.PolicyHash)
        {
            Fail("reissue-candidate-revision-mismatch");
        }
        _ = PythonRuntimeHostProfileDocument.Load(
            candidates.ProfileDocument.Span);
        _ = RuntimeHostAuthorizationPolicyFile.Load(
            candidates.AuthorizationPolicyDocument.Span);
        _ = RuntimeHostClientCredentialEnrollmentRegistryFile.Load(
            candidates.FinalEnrollmentDocument.Span);
    }

    private static void ValidatePublished(Journal journal)
    {
        foreach (Entry entry in journal.Entries)
        {
            EnsureHash(entry.TargetPath, entry.CandidateSha256,
                "published-candidate-invalid");
            EnsureHash(entry.BackupPath, entry.OriginalSha256,
                "retained-source-invalid");
        }
    }

    private static void ValidateFinalEnrollment(Journal journal, string path)
    {
        RuntimeHostClientCredentialEnrollmentRegistry registry =
            RuntimeHostClientCredentialEnrollmentRegistryFile.Load(
                File.ReadAllBytes(path));
        if (Resolve(registry, journal.ExpiredCredentialId)
            || !Resolve(registry, journal.ReplacementCredentialId))
            Fail("final-enrollment-invalid");
    }

    private static bool Resolve(
        RuntimeHostClientCredentialEnrollmentRegistry registry,
        string credentialId)
    {
        var identity = new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(credentialId));
        return registry.TryResolve(identity, DateTimeOffset.UtcNow, out _);
    }

    private static void ValidateRecoverable(Journal journal)
    {
        foreach (Entry entry in journal.Entries)
        {
            string target = ExistingHash(entry.TargetPath);
            string backup = ExistingHash(entry.BackupPath);
            string stage = ExistingHash(entry.StagePath);
            bool beforePublication = target == entry.OriginalSha256
                && string.IsNullOrEmpty(backup)
                && (string.IsNullOrEmpty(stage) || stage == entry.CandidateSha256);
            bool duringOrAfterPublication = backup == entry.OriginalSha256
                && (string.IsNullOrEmpty(target) || target == entry.CandidateSha256)
                && (string.IsNullOrEmpty(stage) || stage == entry.CandidateSha256);
            if (!beforePublication && !duringOrAfterPublication)
                Fail("reissue-recovery-state-ambiguous");
        }
    }

    private static void RollBack(Journal journal, string journalPath)
    {
        foreach (Entry entry in journal.Entries.Reverse())
        {
            if (ExistingHash(entry.BackupPath) == entry.OriginalSha256)
            {
                if (File.Exists(entry.TargetPath)) File.Delete(entry.TargetPath);
                File.Move(entry.BackupPath, entry.TargetPath);
                RestoreSecurity(entry.TargetPath, entry.Security);
            }
        }
        Cleanup(journal, journalPath);
    }

    private static void Cleanup(Journal journal, string journalPath)
    {
        foreach (Entry entry in journal.Entries)
        {
            File.Delete(entry.StagePath);
            File.Delete(entry.BackupPath);
        }
        File.Delete(journalPath + ".next");
        File.Delete(journalPath);
    }

    private void Reach(PythonCredentialReissuePublicationStep step) =>
        stepReached?.Invoke(step);

    private static void TryRollBack(ValidatedRequest request)
    {
        try
        {
            string path = Path.Combine(request.Directory, JournalName);
            if (!File.Exists(path)) return;
            Journal journal = ReadJournal(path, request);
            ValidateRecoverable(journal);
            RollBack(journal, path);
        }
        catch (Exception)
        {
        }
    }

    private static Entry CreateEntry(
        string target,
        ReadOnlySpan<byte> candidate,
        string id) =>
        new(target, target + ".stage-" + id, target + ".backup-" + id,
            Hash(candidate), ExistingHash(target), CaptureSecurity(target));

    private static async Task ValidateSourcesAsync(
        ValidatedRequest request,
        CancellationToken token)
    {
        await Task.WhenAll(
            VerifyAsync(request.CertificatePath, request.CertificateHash, token),
            VerifyAsync(request.PrivateKeyPath, request.PrivateKeyHash, token),
            VerifyAsync(request.ProfilePath, request.ProfileHash, token),
            VerifyAsync(request.EnrollmentPath, request.EnrollmentHash, token),
            VerifyAsync(request.PolicyPath, request.PolicyHash, token))
            .ConfigureAwait(false);
    }

    private static async Task VerifyAsync(
        string path, string expected, CancellationToken token)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, token)
            .ConfigureAwait(false);
        try
        {
            if (Convert.ToHexStringLower(hash) != expected)
                Fail("reissue-source-revision-mismatch");
        }
        finally { CryptographicOperations.ZeroMemory(hash); }
    }

    private static ValidatedRequest Validate(
        PythonCredentialRotationPublicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string directory = RequireDirectory(request.ProvisioningDirectory);
        string certificate = RequireTarget(directory, request.CertificatePath);
        string privateKey = RequireTarget(directory, request.PrivateKeyPath);
        string profile = RequireTarget(directory, request.ProfilePath);
        string enrollment = RequireFile(request.EnrollmentPath);
        string policy = RequireFile(request.AuthorizationPolicyPath);
        string[] paths = [certificate, privateKey, profile, enrollment, policy];
        if (paths.Distinct(PathComparer).Count() != paths.Length)
            Fail("reissue-paths-not-distinct");
        string[] hashes =
        [
            request.ExpectedCertificateSha256,
            request.ExpectedPrivateKeySha256,
            request.ExpectedProfileSha256,
            request.ExpectedEnrollmentSha256,
            request.ExpectedAuthorizationPolicySha256,
        ];
        if (hashes.Any(hash => hash is null || hash.Length != 64
            || hash.Any(character => character is not (>= '0' and <= '9'
                or >= 'a' and <= 'f'))))
            Fail("reissue-hash-invalid");
        return new(directory, certificate, privateKey, profile, enrollment,
            policy, hashes[0], hashes[1], hashes[2], hashes[3], hashes[4]);
    }

    private static string RequireDirectory(string value)
    {
        string path = RequireAbsolute(value);
        if (!Directory.Exists(path) || Reparse(path))
            Fail("reissue-directory-invalid");
        return path;
    }

    private static string RequireTarget(string root, string value)
    {
        string path = RequireFile(value);
        string relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar))
            Fail("reissue-target-outside-directory");
        return path;
    }

    private static string RequireFile(string value)
    {
        string path = RequireAbsolute(value);
        if (!File.Exists(path) || Reparse(path)) Fail("reissue-file-invalid");
        return path;
    }

    private static string RequireAbsolute(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim()
            || !Path.IsPathFullyQualified(value))
            Fail("reissue-path-invalid");
        return Path.GetFullPath(value);
    }

    private static Journal ReadJournal(string path, ValidatedRequest request)
    {
        if (!File.Exists(path) || Reparse(path)) Fail("reissue-journal-missing");
        byte[] journalBytes = File.ReadAllBytes(path);
        Journal? journal;
        try
        {
            using JsonDocument parsed = JsonDocument.Parse(journalBytes);
            RejectDuplicateJsonProperties(parsed.RootElement);
            journal = JsonSerializer.Deserialize<Journal>(journalBytes,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = false,
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                });
        }
        catch (JsonException)
        {
            journal = null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(journalBytes);
        }
        string[] expectedTargets =
        [
            request.CertificatePath, request.PrivateKeyPath,
            request.ProfilePath, request.EnrollmentPath,
        ];
        string[] expectedOriginals =
        [
            request.CertificateHash, request.PrivateKeyHash,
            request.ProfileHash, request.EnrollmentHash,
        ];
        if (journal is null
            || journal.Entries is null
            || journal.Entries.Count != 4
            || journal.AuthorizationPolicyPath != request.PolicyPath
            || journal.AuthorizationPolicySha256 != request.PolicyHash
            || journal.TransactionId is null
            || journal.TransactionId.Length != 32
            || journal.TransactionId.Any(character =>
                character is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            || journal.Phase is not ("created" or "staged"
                or "published-1" or "published-2" or "published-3"
                or "published-4" or "committed")
            || journal.Entries.Where((entry, index) =>
                    !PathComparer.Equals(entry.TargetPath,
                        expectedTargets[index])
                    || entry.OriginalSha256 != expectedOriginals[index])
                .Any())
            Fail("reissue-journal-invalid");
        foreach (Entry entry in journal.Entries)
        {
            if (!PathComparer.Equals(entry.StagePath,
                    entry.TargetPath + ".stage-" + journal.TransactionId)
                || !PathComparer.Equals(entry.BackupPath,
                    entry.TargetPath + ".backup-" + journal.TransactionId)
                || entry.CandidateSha256 is not { Length: 64 }
                || string.IsNullOrEmpty(entry.Security))
                Fail("reissue-journal-invalid");
        }
        return journal;
    }

    private static void RejectDuplicateJsonProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) Fail("reissue-journal-invalid");
                RejectDuplicateJsonProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
                RejectDuplicateJsonProperties(item);
        }
    }

    private static async Task WriteJournalAsync(
        string path, Journal journal, CancellationToken token)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(journal,
            new JsonSerializerOptions { WriteIndented = true });
        try
        {
            string next = path + ".next";
            await using (var stream = new FileStream(next, FileMode.Create,
                FileAccess.Write, FileShare.None, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            File.Move(next, path, overwrite: true);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static string ExistingHash(string path)
    {
        if (!File.Exists(path)) return string.Empty;
        byte[] bytes = File.ReadAllBytes(path);
        try { return Hash(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static void EnsureHash(string path, string expected, string code)
    {
        if (ExistingHash(path) != expected) Fail(code);
    }

    private static string Hash(ReadOnlySpan<byte> value)
    {
        byte[] hash = SHA256.HashData(value);
        try { return Convert.ToHexStringLower(hash); }
        finally { CryptographicOperations.ZeroMemory(hash); }
    }

    private static string CaptureSecurity(string path)
    {
        if (OperatingSystem.IsWindows())
            return new FileInfo(path).GetAccessControl()
                .GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        return ((int)File.GetUnixFileMode(path)).ToString();
    }

    private static void RestoreSecurity(string path, string security)
    {
        if (OperatingSystem.IsWindows())
        {
            var descriptor = new FileSecurity();
            descriptor.SetSecurityDescriptorSddlForm(
                security, AccessControlSections.Access);
            new FileInfo(path).SetAccessControl(descriptor);
        }
        else File.SetUnixFileMode(path, (UnixFileMode)int.Parse(security));
    }

    private static bool Reparse(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    [DoesNotReturn]
    private static void Fail(string code) =>
        throw new PythonCredentialRotationPublicationException(code);

    private sealed record ValidatedRequest(
        string Directory,
        string CertificatePath,
        string PrivateKeyPath,
        string ProfilePath,
        string EnrollmentPath,
        string PolicyPath,
        string CertificateHash,
        string PrivateKeyHash,
        string ProfileHash,
        string EnrollmentHash,
        string PolicyHash);

    private sealed record Journal(
        string TransactionId,
        string Phase,
        string AuthorizationPolicyPath,
        string AuthorizationPolicySha256,
        string ExpiredCredentialId,
        string ReplacementCredentialId,
        IReadOnlyList<Entry> Entries);

    private sealed record Entry(
        string TargetPath,
        string StagePath,
        string BackupPath,
        string CandidateSha256,
        string OriginalSha256,
        string Security);
}

internal enum PythonCredentialReissuePublicationStep
{
    Staged = 1,
    CertificatePublished = 2,
    PrivateKeyPublished = 3,
    ProfilePublished = 4,
    EnrollmentPublished = 5,
    Committed = 6,
}
