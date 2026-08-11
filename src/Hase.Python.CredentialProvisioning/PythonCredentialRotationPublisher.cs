using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.Python.CredentialProvisioning;

/// <summary>
/// Publishes the local half of one planned rotation as a durable transaction.
/// Begin retains exact old files until explicit finalization or rollback.
/// </summary>
public sealed class PythonCredentialRotationPublisher
{
    private const string JournalName =
        "python-credential-rotation.transaction.json";
    private readonly Action<PythonCredentialRotationPublicationStep>?
        stepReached;

    public PythonCredentialRotationPublisher()
    {
    }

    internal PythonCredentialRotationPublisher(
        Action<PythonCredentialRotationPublicationStep> stepReached)
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
        if (File.Exists(journalPath)) Fail("rotation-recovery-required");

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
                candidates.OverlapEnrollmentDocument.Span, id),
        ];
        string finalStage = validated.EnrollmentPath + ".final-stage-" + id;
        var journal = new Journal(
            id, "created", validated.PolicyPath,
            validated.PolicyHash,
            candidates.CurrentCredentialId,
            candidates.ReplacementCredentialId,
            finalStage,
            Hash(candidates.FinalEnrollmentDocument.Span),
            entries);

        try
        {
            await WriteJournalAsync(journalPath, journal, cancellationToken)
                .ConfigureAwait(false);
            await StageAsync(entries, candidates, finalStage, cancellationToken)
                .ConfigureAwait(false);
            journal = journal with { Phase = "staged" };
            await WriteJournalAsync(journalPath, journal, cancellationToken)
                .ConfigureAwait(false);
            Reach(PythonCredentialRotationPublicationStep.Staged);

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
                Reach((PythonCredentialRotationPublicationStep)(index + 2));
            }

            ValidatePublished(journal, requireFinal: false);
            journal = journal with { Phase = "overlap-published" };
            await WriteJournalAsync(journalPath, journal, cancellationToken)
                .ConfigureAwait(false);
            Reach(PythonCredentialRotationPublicationStep.OverlapPublished);
            return new(id, "OverlapPublished", RollbackRetained: true);
        }
        catch (OperationCanceledException)
        {
            TryRecover(validated);
            throw;
        }
        catch (PythonCredentialRotationPublicationException)
        {
            TryRecover(validated);
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
            TryRecover(validated);
            throw new PythonCredentialRotationPublicationException(
                "rotation-publication-failed");
        }
    }

    public PythonCredentialRotationPublicationResult Finalize(
        PythonCredentialRotationPublicationRequest request)
    {
        ValidatedRequest validated = Validate(request);
        string journalPath = Path.Combine(validated.Directory, JournalName);
        Journal journal = ReadJournal(journalPath, validated);
        if (journal.Phase != "overlap-published")
            Fail("rotation-finalization-not-ready");
        ValidatePublished(journal, requireFinal: false);
        EnsureHash(validated.PolicyPath, validated.PolicyHash,
            "authorization-policy-revision-mismatch");

        string enrollmentBackup = validated.EnrollmentPath
            + ".overlap-backup-" + journal.TransactionId;
        try
        {
            File.Move(validated.EnrollmentPath, enrollmentBackup);
            File.Move(journal.FinalEnrollmentStagePath,
                validated.EnrollmentPath);
            RestoreSecurity(validated.EnrollmentPath,
                journal.Entries[3].Security);
            EnsureHash(validated.EnrollmentPath,
                journal.FinalEnrollmentSha256,
                "final-enrollment-invalid");
            ValidateFinalEnrollment(journal, validated.EnrollmentPath);
            journal = journal with { Phase = "committed" };
            WriteJournal(journalPath, journal);
            File.Delete(enrollmentBackup);
            Cleanup(journal, journalPath);
            return new(journal.TransactionId, "Finalized",
                RollbackRetained: false);
        }
        catch (PythonCredentialRotationPublicationException)
        {
            RestoreOverlap(validated.EnrollmentPath, enrollmentBackup,
                journal);
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or CryptographicException
            or InvalidDataException
            or JsonException
            or ArgumentException)
        {
            RestoreOverlap(validated.EnrollmentPath, enrollmentBackup,
                journal);
            throw new PythonCredentialRotationPublicationException(
                "rotation-finalization-failed");
        }
    }

    public PythonCredentialRotationPublicationResult Recover(
        PythonCredentialRotationPublicationRequest request)
    {
        ValidatedRequest validated = Validate(request);
        string journalPath = Path.Combine(validated.Directory, JournalName);
        Journal journal = ReadJournal(journalPath, validated);
        if (journal.Phase == "committed")
        {
            ValidatePublished(journal, requireFinal: true);
            Cleanup(journal, journalPath);
            return new(journal.TransactionId, "CommittedCleanup",
                RollbackRetained: false);
        }

        RestoreOverlap(validated.EnrollmentPath,
            validated.EnrollmentPath + ".overlap-backup-"
                + journal.TransactionId,
            journal);
        ValidateRecoverable(journal);
        RollBack(journal, journalPath);
        return new(journal.TransactionId, "RolledBack",
            RollbackRetained: false);
    }

    private static async Task StageAsync(
        Entry[] entries,
        PythonCredentialRotationCandidates candidates,
        string finalStage,
        CancellationToken token)
    {
        ReadOnlyMemory<byte>[] values =
        [
            candidates.ReplacementCertificatePem,
            candidates.ReplacementPrivateKeyPem,
            candidates.ProfileDocument,
            candidates.OverlapEnrollmentDocument,
        ];
        for (int index = 0; index < entries.Length; index++)
        {
            await WriteNewAsync(entries[index].StagePath, values[index], token)
                .ConfigureAwait(false);
            RestoreSecurity(entries[index].StagePath, entries[index].Security);
            EnsureHash(entries[index].StagePath, entries[index].CandidateSha256,
                "staged-candidate-invalid");
        }
        await WriteNewAsync(finalStage,
            candidates.FinalEnrollmentDocument, token).ConfigureAwait(false);
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
            Fail("rotation-candidate-revision-mismatch");
        }
        _ = PythonRuntimeHostProfileDocument.Load(
            candidates.ProfileDocument.Span);
        _ = RuntimeHostAuthorizationPolicyFile.Load(
            candidates.AuthorizationPolicyDocument.Span);
        _ = RuntimeHostClientCredentialEnrollmentRegistryFile.Load(
            candidates.OverlapEnrollmentDocument.Span);
        _ = RuntimeHostClientCredentialEnrollmentRegistryFile.Load(
            candidates.FinalEnrollmentDocument.Span);
    }

    private static void ValidatePublished(Journal journal, bool requireFinal)
    {
        for (int index = 0; index < journal.Entries.Count; index++)
        {
            string expected = requireFinal && index == 3
                ? journal.FinalEnrollmentSha256
                : journal.Entries[index].CandidateSha256;
            EnsureHash(journal.Entries[index].TargetPath, expected,
                "published-candidate-invalid");
        }
        if (!requireFinal)
        {
            EnsureHash(journal.FinalEnrollmentStagePath,
                journal.FinalEnrollmentSha256, "final-stage-invalid");
        }
    }

    private static void ValidateFinalEnrollment(Journal journal, string path)
    {
        RuntimeHostClientCredentialEnrollmentRegistry registry =
            RuntimeHostClientCredentialEnrollmentRegistryFile.Load(
                File.ReadAllBytes(path));
        bool oldPresent = Resolve(registry, journal.CurrentCredentialId);
        bool replacementPresent = Resolve(registry,
            journal.ReplacementCredentialId);
        if (oldPresent || !replacementPresent)
            Fail("final-enrollment-invalid");
    }

    private static bool Resolve(
        RuntimeHostClientCredentialEnrollmentRegistry registry,
        string credentialId)
    {
        var identity = new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(credentialId));
        return registry.TryResolve(identity, DateTimeOffset.UtcNow,
            out _);
    }

    private static void ValidateRecoverable(Journal journal)
    {
        foreach (Entry entry in journal.Entries)
        {
            string target = ExistingHash(entry.TargetPath);
            string backup = ExistingHash(entry.BackupPath);
            string stage = ExistingHash(entry.StagePath);
            bool originalAtTarget = target == entry.OriginalSha256;
            bool originalInBackup = backup == entry.OriginalSha256;
            bool candidateAtTarget = target == entry.CandidateSha256;
            bool candidateInStage = stage == entry.CandidateSha256;
            bool beforePublication = originalAtTarget
                && string.IsNullOrEmpty(backup)
                && (string.IsNullOrEmpty(stage) || candidateInStage);
            bool duringOrAfterPublication = originalInBackup
                && (string.IsNullOrEmpty(target) || candidateAtTarget)
                && (string.IsNullOrEmpty(stage) || candidateInStage);
            if (!beforePublication && !duringOrAfterPublication)
            {
                Fail("rotation-recovery-state-ambiguous");
            }
        }
        string finalHash = ExistingHash(journal.FinalEnrollmentStagePath);
        if (!string.IsNullOrEmpty(finalHash)
            && finalHash != journal.FinalEnrollmentSha256)
            Fail("rotation-recovery-state-ambiguous");
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
        File.Delete(journal.FinalEnrollmentStagePath);
        File.Delete(journal.Entries[3].TargetPath + ".overlap-backup-"
            + journal.TransactionId);
        File.Delete(journalPath + ".next");
        File.Delete(journalPath);
    }

    private static void RestoreOverlap(
        string enrollmentPath,
        string backupPath,
        Journal journal)
    {
        try
        {
            if (ExistingHash(backupPath) == journal.Entries[3].CandidateSha256)
            {
                if (File.Exists(enrollmentPath)) File.Delete(enrollmentPath);
                File.Move(backupPath, enrollmentPath);
                RestoreSecurity(enrollmentPath, journal.Entries[3].Security);
            }
        }
        catch (Exception)
        {
        }
    }

    private void Reach(PythonCredentialRotationPublicationStep step) =>
        stepReached?.Invoke(step);

    private static void TryRecover(ValidatedRequest request)
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
                Fail("rotation-source-revision-mismatch");
        }
        finally { CryptographicOperations.ZeroMemory(hash); }
    }

    private static ValidatedRequest Validate(
        PythonCredentialRotationPublicationRequest request)
    {
        string directory = RequireDirectory(request.ProvisioningDirectory);
        string certificate = RequireTarget(directory, request.CertificatePath);
        string privateKey = RequireTarget(directory, request.PrivateKeyPath);
        string profile = RequireTarget(directory, request.ProfilePath);
        string enrollment = RequireFile(request.EnrollmentPath);
        string policy = RequireFile(request.AuthorizationPolicyPath);
        string[] paths = [certificate, privateKey, profile, enrollment, policy];
        if (paths.Distinct(PathComparer).Count() != paths.Length)
            Fail("rotation-paths-not-distinct");
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
            Fail("rotation-hash-invalid");
        return new(directory, certificate, privateKey, profile, enrollment,
            policy, hashes[0], hashes[1], hashes[2], hashes[3], hashes[4]);
    }

    private static string RequireDirectory(string value)
    {
        string path = RequireAbsolute(value);
        if (!Directory.Exists(path) || Reparse(path))
            Fail("rotation-directory-invalid");
        return path;
    }

    private static string RequireTarget(string root, string value)
    {
        string path = RequireFile(value);
        string relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar))
            Fail("rotation-target-outside-directory");
        return path;
    }

    private static string RequireFile(string value)
    {
        string path = RequireAbsolute(value);
        if (!File.Exists(path) || Reparse(path)) Fail("rotation-file-invalid");
        return path;
    }

    private static string RequireAbsolute(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim()
            || !Path.IsPathFullyQualified(value))
            Fail("rotation-path-invalid");
        return Path.GetFullPath(value);
    }

    private static Journal ReadJournal(
        string path, ValidatedRequest request)
    {
        if (!File.Exists(path) || Reparse(path)) Fail("rotation-journal-missing");
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
            || journal.Entries.Count != 4
            || journal.AuthorizationPolicyPath != request.PolicyPath
            || journal.AuthorizationPolicySha256 != request.PolicyHash
            || journal.TransactionId.Length != 32
            || journal.TransactionId.Any(character =>
                character is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            || journal.Phase is not ("created" or "staged"
                or "published-1" or "published-2" or "published-3"
                or "published-4" or "overlap-published" or "committed")
            || !PathComparer.Equals(journal.FinalEnrollmentStagePath,
                request.EnrollmentPath + ".final-stage-"
                    + journal.TransactionId)
            || journal.Entries.Where((entry, index) =>
                    !PathComparer.Equals(entry.TargetPath,
                        expectedTargets[index])
                    || entry.OriginalSha256 != expectedOriginals[index])
                .Any())
            Fail("rotation-journal-invalid");
        for (int index = 0; index < journal.Entries.Count; index++)
        {
            Entry entry = journal.Entries[index];
            if (!PathComparer.Equals(entry.StagePath,
                    entry.TargetPath + ".stage-" + journal.TransactionId)
                || !PathComparer.Equals(entry.BackupPath,
                    entry.TargetPath + ".backup-" + journal.TransactionId)
                || entry.CandidateSha256.Length != 64
                || entry.Security.Length == 0)
                Fail("rotation-journal-invalid");
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
                if (!names.Add(property.Name)) Fail("rotation-journal-invalid");
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

    private static void WriteJournal(string path, Journal journal)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(journal,
            new JsonSerializerOptions { WriteIndented = true });
        try
        {
            string next = path + ".next";
            using (var stream = new FileStream(next, FileMode.Create,
                FileAccess.Write, FileShare.None, 4096,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes);
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
        string CurrentCredentialId,
        string ReplacementCredentialId,
        string FinalEnrollmentStagePath,
        string FinalEnrollmentSha256,
        IReadOnlyList<Entry> Entries);

    private sealed record Entry(
        string TargetPath,
        string StagePath,
        string BackupPath,
        string CandidateSha256,
        string OriginalSha256,
        string Security);
}

internal enum PythonCredentialRotationPublicationStep
{
    Staged = 1,
    CertificatePublished = 2,
    PrivateKeyPublished = 3,
    ProfilePublished = 4,
    OverlapEnrollmentFilePublished = 5,
    OverlapPublished = 6,
}
