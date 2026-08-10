using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.Json;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.Python.CredentialProvisioning;

public sealed class PythonCredentialProvisioningPublisher
{
    private readonly Action<PythonCredentialPublicationStep>? stepReached;

    public PythonCredentialProvisioningPublisher()
    {
    }

    internal PythonCredentialProvisioningPublisher(
        Action<PythonCredentialPublicationStep> stepReached)
    {
        this.stepReached = stepReached;
    }

    public async Task<PythonCredentialProvisioningPublicationResult> PublishAsync(
        PythonCredentialProvisioningPlan plan,
        PythonCredentialProvisioningCandidates candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(candidates);

        using var personalStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        using var trustedStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        personalStore.Open(OpenFlags.ReadOnly);
        trustedStore.Open(OpenFlags.ReadOnly);

        return await PublishAsync(
            plan,
            candidates,
            personalStore.Certificates.Cast<X509Certificate2>(),
            trustedStore.Certificates.Cast<X509Certificate2>(),
            DateTimeOffset.UtcNow,
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<PythonCredentialProvisioningPublicationResult> PublishAsync(
        PythonCredentialProvisioningPlan plan,
        PythonCredentialProvisioningCandidates candidates,
        IEnumerable<X509Certificate2> personalCertificates,
        IEnumerable<X509Certificate2> trustedRoots,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(personalCertificates);
        ArgumentNullException.ThrowIfNull(trustedRoots);

        byte[]? certificate = null;
        byte[]? privateKey = null;
        byte[]? profile = null;
        byte[]? enrollment = null;
        byte[]? policy = null;
        Transaction? transaction = null;
        bool committed = false;

        try
        {
            PythonCredentialProvisioningPreparer.ValidatePlan(plan);
            if (utcNow.Offset != TimeSpan.Zero
                || utcNow < plan.NotBeforeUtc
                || utcNow >= plan.NotAfterUtc)
            {
                Fail("credential-validity-invalid");
            }
            cancellationToken.ThrowIfCancellationRequested();
            certificate = candidates.CertificatePem.ToArray();
            privateKey = candidates.PrivateKeyPem.ToArray();
            profile = candidates.ProfileDocument.ToArray();
            enrollment = candidates.EnrollmentDocument.ToArray();
            policy = candidates.AuthorizationPolicyDocument.ToArray();

            await ValidateLockedInputsAsync(plan, cancellationToken)
                .ConfigureAwait(false);
            X509Certificate2 personalRoot =
                PythonCredentialProvisioningPreparer.SelectExactRoot(
                    plan.SigningRootThumbprint,
                    personalCertificates,
                    requirePrivateKey: true);
            X509Certificate2 trustedRoot =
                PythonCredentialProvisioningPreparer.SelectExactRoot(
                    plan.SigningRootThumbprint,
                    trustedRoots,
                    requirePrivateKey: false);
            if (!personalRoot.RawData.AsSpan().SequenceEqual(trustedRoot.RawData)
                || !string.Equals(
                    PythonCredentialProvisioningPreparer.CalculatePlanId(
                        plan,
                        trustedRoot),
                    plan.PlanId,
                    StringComparison.Ordinal))
            {
                Fail("plan-revision-invalid");
            }

            PythonCredentialProvisioningPreparer.ValidateCredential(
                plan,
                certificate,
                privateKey,
                trustedRoot);
            ValidateCandidateDocuments(plan, profile, enrollment, policy);

            transaction = CreateTransaction(
                plan,
                certificate,
                privateKey,
                profile,
                enrollment,
                policy);
            ValidateTargets(plan, transaction.Entries);
            WriteJournal(transaction, "created");
            Reach(PythonCredentialPublicationStep.JournalDurable);
            Stage(transaction);
            WriteJournal(transaction, "staged");
            Reach(PythonCredentialPublicationStep.Staged);

            for (int index = 0; index < transaction.Entries.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteJournal(transaction, $"publishing-{index + 1}");
                PublishEntry(transaction.Entries[index]);
                WriteJournal(transaction, $"published-{index + 1}");
                Reach((PythonCredentialPublicationStep)(index + 3));
            }

            WriteJournal(transaction, "committed");
            committed = true;
            CleanupCommitted(transaction);
            return new PythonCredentialProvisioningPublicationResult(
                transaction.TransactionId,
                plan.CertificatePath,
                plan.PrivateKeyPath,
                plan.ProfilePath,
                plan.EnrollmentPath,
                plan.AuthorizationPolicyPath,
                transaction.Entries.Take(3).Any(entry => entry.TargetExisted));
        }
        catch (PythonCredentialProvisioningPublicationException)
        {
            HandleFailure(transaction, committed);
            throw;
        }
        catch (PythonCredentialProvisioningPreparationException)
        {
            HandleFailure(transaction, committed);
            throw new PythonCredentialProvisioningPublicationException(
                "preparation-revalidation-failed");
        }
        catch (OperationCanceledException)
        {
            HandleFailure(transaction, committed);
            throw;
        }
        catch (Exception exception) when (exception is SystemException)
        {
            HandleFailure(transaction, committed);
            throw new PythonCredentialProvisioningPublicationException(
                committed ? "committed-cleanup-failed" : "transaction-failed");
        }
        finally
        {
            Zero(certificate);
            Zero(privateKey);
            Zero(profile);
            Zero(enrollment);
            Zero(policy);
        }
    }

    private static async Task ValidateLockedInputsAsync(
        PythonCredentialProvisioningPlan plan,
        CancellationToken cancellationToken)
    {
        await ValidateHashAsync(plan.SourceProfilePath, plan.SourceProfileSha256,
            cancellationToken).ConfigureAwait(false);
        await ValidateHashAsync(plan.EnrollmentPath, plan.EnrollmentSha256,
            cancellationToken).ConfigureAwait(false);
        await ValidateHashAsync(plan.AuthorizationPolicyPath,
            plan.AuthorizationPolicySha256, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ValidateHashAsync(
        string path,
        string expected,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            if (!string.Equals(Convert.ToHexStringLower(hash), expected,
                StringComparison.Ordinal))
            {
                Fail("input-revision-mismatch");
            }
        }
        finally
        {
            Zero(hash);
        }
    }

    private static void ValidateCandidateDocuments(
        PythonCredentialProvisioningPlan plan,
        byte[] profile,
        byte[] enrollment,
        byte[] policy)
    {
        byte[]? sourceProfileBytes = null;
        byte[]? sourceEnrollmentBytes = null;
        byte[]? sourcePolicyBytes = null;
        try
        {
            sourceProfileBytes = ReadAndValidateHash(
                plan.SourceProfilePath,
                plan.SourceProfileSha256);
            sourceEnrollmentBytes = ReadAndValidateHash(
                plan.EnrollmentPath,
                plan.EnrollmentSha256);
            sourcePolicyBytes = ReadAndValidateHash(
                plan.AuthorizationPolicyPath,
                plan.AuthorizationPolicySha256);
            PythonRuntimeHostProfileDocument parsedProfile =
                PythonRuntimeHostProfileDocument.Load(profile);
            PythonRuntimeHostProfileDocument sourceProfile =
                PythonRuntimeHostProfileDocument.Load(sourceProfileBytes);
            if (!PathComparer.Equals(parsedProfile.ClientCertificateChainPath,
                    plan.CertificatePath)
                || !PathComparer.Equals(parsedProfile.ClientPrivateKeyPath,
                    plan.PrivateKeyPath)
                || parsedProfile.Address != sourceProfile.Address
                || !PathComparer.Equals(parsedProfile.TrustedServerCertificatePath,
                    sourceProfile.TrustedServerCertificatePath))
            {
                Fail("candidate-profile-invalid");
            }

            _ = RuntimeHostClientCredentialEnrollmentRegistryFile.Load(enrollment);
            _ = RuntimeHostAuthorizationPolicyFile.Load(policy);
            using JsonDocument sourceEnrollment = JsonDocument.Parse(
                sourceEnrollmentBytes);
            using JsonDocument candidateEnrollment = JsonDocument.Parse(enrollment);
            using JsonDocument sourcePolicy = JsonDocument.Parse(sourcePolicyBytes);
            using JsonDocument candidatePolicy = JsonDocument.Parse(policy);

            JsonElement[] oldEnrollmentArray = sourceEnrollment.RootElement
                .GetProperty("enrollments").EnumerateArray().ToArray();
            JsonElement[] newEnrollmentArray = candidateEnrollment.RootElement
                .GetProperty("enrollments").EnumerateArray().ToArray();
            if (newEnrollmentArray.Length != oldEnrollmentArray.Length + 1
                || !oldEnrollmentArray.Zip(newEnrollmentArray)
                    .All(pair => JsonElement.DeepEquals(pair.First, pair.Second)))
            {
                Fail("candidate-enrollment-invalid");
            }
            JsonElement addedEnrollment = newEnrollmentArray[^1];
            if (addedEnrollment.GetProperty("credentialId").GetString()
                    != plan.CredentialId
                || addedEnrollment.GetProperty("principalId").GetString()
                    != plan.PrincipalId
                || addedEnrollment.GetProperty("trustPolicyId").GetString()
                    != plan.TrustPolicyId)
            {
                Fail("candidate-enrollment-invalid");
            }

            JsonElement[] oldGrants = sourcePolicy.RootElement
                .GetProperty("grants").EnumerateArray().ToArray();
            JsonElement[] newGrants = candidatePolicy.RootElement
                .GetProperty("grants").EnumerateArray().ToArray();
            if (newGrants.Length != oldGrants.Length + 2
                || !oldGrants.Zip(newGrants)
                    .All(pair => JsonElement.DeepEquals(pair.First, pair.Second)))
            {
                Fail("candidate-policy-invalid");
            }
            string[] permissions = newGrants.Skip(oldGrants.Length)
                .Select(grant =>
                {
                    if (grant.GetProperty("principalId").GetString()
                        != plan.PrincipalId)
                    {
                        Fail("candidate-policy-invalid");
                    }
                    return grant.GetProperty("permission").GetString()!;
                }).ToArray();
            if (!permissions.SequenceEqual(plan.AuthorizationGrants))
            {
                Fail("candidate-policy-invalid");
            }
        }
        finally
        {
            Zero(sourceProfileBytes);
            Zero(sourceEnrollmentBytes);
            Zero(sourcePolicyBytes);
        }
    }

    private static byte[] ReadAndValidateHash(string path, string expected)
    {
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        try
        {
            if (!string.Equals(Convert.ToHexStringLower(hash), expected,
                StringComparison.Ordinal))
            {
                Zero(bytes);
                Fail("input-revision-mismatch");
            }
            return bytes;
        }
        finally
        {
            Zero(hash);
        }
    }

    private static Transaction CreateTransaction(
        PythonCredentialProvisioningPlan plan,
        byte[] certificate,
        byte[] privateKey,
        byte[] profile,
        byte[] enrollment,
        byte[] policy)
    {
        string transactionId = Guid.NewGuid().ToString("N");
        string journalPath = Path.Combine(
            plan.ProvisioningDirectory,
            $".hase-python-provisioning-{transactionId}.journal.json");
        var entries = new List<Entry>
        {
            CreateEntry(plan.CertificatePath, certificate, transactionId),
            CreateEntry(plan.PrivateKeyPath, privateKey, transactionId),
            CreateEntry(plan.ProfilePath, profile, transactionId),
            CreateEntry(plan.EnrollmentPath, enrollment, transactionId),
            CreateEntry(plan.AuthorizationPolicyPath, policy, transactionId),
        };
        return new Transaction(
            transactionId,
            plan.PlanId,
            plan.SourceProfileSha256,
            plan.EnrollmentSha256,
            plan.AuthorizationPolicySha256,
            journalPath,
            entries);
    }

    private static Entry CreateEntry(string target, byte[] content, string id)
    {
        bool existed = File.Exists(target);
        return new Entry(
            target,
            target + ".stage-" + id,
            target + ".backup-" + id,
            content,
            HashHex(content),
            existed,
            existed ? HashFileHex(target) : null,
            existed ? CaptureSecurity(target) : null);
    }

    private static void ValidateTargets(
        PythonCredentialProvisioningPlan plan,
        IReadOnlyList<Entry> entries)
    {
        if (!Directory.Exists(plan.ProvisioningDirectory)
            || IsReparsePoint(plan.ProvisioningDirectory)
            || Directory.EnumerateFiles(
                    plan.ProvisioningDirectory,
                    ".hase-python-provisioning-*.journal.json*")
                .Any()
            || entries.Select(entry => Path.GetFullPath(entry.TargetPath))
                .Distinct(PathComparer).Count() != entries.Count)
        {
            Fail("publication-target-invalid");
        }
        if (!string.Equals(
                entries[3].OriginalSha256,
                plan.EnrollmentSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                entries[4].OriginalSha256,
                plan.AuthorizationPolicySha256,
                StringComparison.Ordinal))
        {
            Fail("input-revision-mismatch");
        }

        for (int index = 0; index < entries.Count; index++)
        {
            Entry entry = entries[index];
            string? parent = Path.GetDirectoryName(entry.TargetPath);
            if (parent is null || !Directory.Exists(parent)
                || PathContainsReparsePoint(entry.TargetPath)
                || Directory.Exists(entry.TargetPath)
                || (entry.TargetExisted && IsReparsePoint(entry.TargetPath)))
            {
                Fail("publication-target-invalid");
            }
            if (index < 3 && entry.TargetExisted && !plan.AllowReplacement)
            {
                Fail("replacement-not-authorized");
            }
            if (index >= 3 && !entry.TargetExisted)
            {
                Fail("security-input-unavailable");
            }
        }
    }

    private static void Stage(Transaction transaction)
    {
        foreach (Entry entry in transaction.Entries)
        {
            WriteRestrictedDurableFile(entry.StagePath, entry.Content);
        }
    }

    private static void PublishEntry(Entry entry)
    {
        if (entry.TargetExisted)
        {
            if (!File.Exists(entry.TargetPath)
                || !string.Equals(
                    HashFileHex(entry.TargetPath),
                    entry.OriginalSha256,
                    StringComparison.Ordinal))
            {
                Fail("target-revision-mismatch");
            }
            File.Replace(
                entry.StagePath,
                entry.TargetPath,
                entry.BackupPath,
                ignoreMetadataErrors: false);
            entry.BackupCreated = true;
            entry.Published = true;
            Restrict(entry.TargetPath);
            Restrict(entry.BackupPath);
        }
        else
        {
            File.Move(entry.StagePath, entry.TargetPath);
            entry.Published = true;
        }
    }

    private static void WriteJournal(Transaction transaction, string phase)
    {
        byte[] document = JsonSerializer.SerializeToUtf8Bytes(new
        {
            formatVersion = 1,
            transactionId = transaction.TransactionId,
            planId = transaction.PlanId,
            phase,
            sourceRevisions = new
            {
                sourceProfileSha256 = transaction.SourceProfileSha256,
                enrollmentSha256 = transaction.EnrollmentSha256,
                authorizationPolicySha256 = transaction.AuthorizationPolicySha256,
            },
            entries = transaction.Entries.Select(entry => new
            {
                targetPath = entry.TargetPath,
                stagePath = entry.StagePath,
                backupPath = entry.BackupPath,
                candidateSha256 = entry.CandidateSha256,
                targetExisted = entry.TargetExisted,
                originalSha256 = entry.OriginalSha256,
                originalSecurity = entry.OriginalSecurity,
                published = entry.Published,
                backupCreated = entry.BackupCreated,
            }),
        }, new JsonSerializerOptions { WriteIndented = true });
        if (document.Length > 64 * 1024)
        {
            Zero(document);
            Fail("journal-too-large");
        }
        string next = transaction.JournalPath + ".next";
        bool replacing = File.Exists(transaction.JournalPath);
        string written = replacing ? next : transaction.JournalPath;
        try
        {
            WriteRestrictedDurableFile(written, document);
            if (replacing)
            {
                File.Move(next, transaction.JournalPath, overwrite: true);
            }
        }
        finally
        {
            Zero(document);
        }
    }

    private static void WriteRestrictedDurableFile(
        string path,
        ReadOnlySpan<byte> content)
    {
        using (new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
        }
        Restrict(path);
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Write, FileShare.None,
            4096, FileOptions.WriteThrough);
        stream.Write(content);
        stream.Flush(flushToDisk: true);
    }

    private static void CleanupCommitted(Transaction transaction)
    {
        foreach (Entry entry in transaction.Entries)
        {
            if (entry.BackupCreated)
            {
                File.Delete(entry.BackupPath);
                entry.BackupCreated = false;
            }
        }
        File.Delete(transaction.JournalPath + ".next");
        File.Delete(transaction.JournalPath);
    }

    private static void HandleFailure(Transaction? transaction, bool committed)
    {
        if (transaction is null || committed)
        {
            return;
        }
        if (!RollBack(transaction))
        {
            throw new PythonCredentialProvisioningPublicationException(
                "rollback-incomplete");
        }
    }

    private static bool RollBack(Transaction transaction)
    {
        bool succeeded = true;
        foreach (Entry entry in transaction.Entries.AsEnumerable().Reverse())
        {
            try
            {
                if (entry.Published && File.Exists(entry.TargetPath))
                {
                    File.Delete(entry.TargetPath);
                }
                if (entry.BackupCreated && File.Exists(entry.BackupPath))
                {
                    File.Move(entry.BackupPath, entry.TargetPath);
                    RestoreSecurity(entry.TargetPath, entry.OriginalSecurity);
                    entry.BackupCreated = false;
                }
                entry.Published = false;
            }
            catch (Exception)
            {
                succeeded = false;
            }
        }
        if (!succeeded)
        {
            return false;
        }
        try
        {
            foreach (Entry entry in transaction.Entries)
            {
                File.Delete(entry.StagePath);
                File.Delete(entry.BackupPath);
            }
            File.Delete(transaction.JournalPath + ".next");
            File.Delete(transaction.JournalPath);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string? CaptureSecurity(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return new FileInfo(path).GetAccessControl()
                .GetSecurityDescriptorSddlForm(
                    AccessControlSections.Access);
        }
        return Convert.ToInt32(File.GetUnixFileMode(path)).ToString();
    }

    private static void RestoreSecurity(string path, string? security)
    {
        if (security is null)
        {
            return;
        }
        if (OperatingSystem.IsWindows())
        {
            var descriptor = new FileSecurity();
            descriptor.SetSecurityDescriptorSddlForm(
                security,
                AccessControlSections.Access);
            new FileInfo(path).SetAccessControl(descriptor);
        }
        else
        {
            File.SetUnixFileMode(path, (UnixFileMode)int.Parse(security));
        }
    }

    private static void Restrict(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            SecurityIdentifier owner = WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException();
            var security = new FileSecurity();
            security.SetOwner(owner);
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(
                owner, FileSystemRights.FullControl, AccessControlType.Allow));
            new FileInfo(path).SetAccessControl(security);
        }
        else
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private void Reach(PythonCredentialPublicationStep step) =>
        stepReached?.Invoke(step);

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static bool PathContainsReparsePoint(string path)
    {
        string? current = File.Exists(path) || Directory.Exists(path)
            ? path
            : Path.GetDirectoryName(path);
        while (current is not null)
        {
            if (IsReparsePoint(current))
            {
                return true;
            }
            string? parent = Path.GetDirectoryName(current);
            if (parent == current)
            {
                break;
            }
            current = parent;
        }
        return false;
    }

    private static void Zero(byte[]? bytes)
    {
        if (bytes is not null)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static string HashHex(ReadOnlySpan<byte> bytes)
    {
        byte[] hash = SHA256.HashData(bytes);
        try
        {
            return Convert.ToHexStringLower(hash);
        }
        finally
        {
            Zero(hash);
        }
    }

    private static string HashFileHex(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        try
        {
            return Convert.ToHexStringLower(hash);
        }
        finally
        {
            Zero(hash);
        }
    }

    private static void Fail(string code) =>
        throw new PythonCredentialProvisioningPublicationException(code);

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private sealed class Transaction(
        string transactionId,
        string planId,
        string sourceProfileSha256,
        string enrollmentSha256,
        string authorizationPolicySha256,
        string journalPath,
        List<Entry> entries)
    {
        public string TransactionId { get; } = transactionId;
        public string PlanId { get; } = planId;
        public string SourceProfileSha256 { get; } = sourceProfileSha256;
        public string EnrollmentSha256 { get; } = enrollmentSha256;
        public string AuthorizationPolicySha256 { get; } = authorizationPolicySha256;
        public string JournalPath { get; } = journalPath;
        public List<Entry> Entries { get; } = entries;
    }

    private sealed class Entry(
        string targetPath,
        string stagePath,
        string backupPath,
        byte[] content,
        string candidateSha256,
        bool targetExisted,
        string? originalSha256,
        string? originalSecurity)
    {
        public string TargetPath { get; } = targetPath;
        public string StagePath { get; } = stagePath;
        public string BackupPath { get; } = backupPath;
        public byte[] Content { get; } = content;
        public string CandidateSha256 { get; } = candidateSha256;
        public bool TargetExisted { get; } = targetExisted;
        public string? OriginalSha256 { get; } = originalSha256;
        public string? OriginalSecurity { get; } = originalSecurity;
        public bool Published { get; set; }
        public bool BackupCreated { get; set; }
    }
}

internal enum PythonCredentialPublicationStep
{
    JournalDurable = 1,
    Staged = 2,
    CertificatePublished = 3,
    PrivateKeyPublished = 4,
    ProfilePublished = 5,
    EnrollmentPublished = 6,
    AuthorizationPolicyPublished = 7,
}
