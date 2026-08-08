using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hase.Python.CredentialProvisioning;

public sealed partial class PythonCredentialProvisioningRecoverer
{
    private const int MaximumJournalBytes = 64 * 1024;

    [GeneratedRegex("\\A[0-9a-f]{32}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex TransactionIdPattern();

    [GeneratedRegex("\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex HashPattern();

    public PythonCredentialProvisioningRecoveryResult Recover(
        PythonCredentialProvisioningRecoveryRequest request)
    {
        try
        {
            return RecoverCore(request);
        }
        catch (PythonCredentialProvisioningRecoveryException)
        {
            throw;
        }
        catch (Exception exception) when (exception is SystemException)
        {
            throw new PythonCredentialProvisioningRecoveryException(
                "recovery-failed");
        }
    }

    private static PythonCredentialProvisioningRecoveryResult RecoverCore(
        PythonCredentialProvisioningRecoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string directory = RequireAbsolute(request.ProvisioningDirectory);
        if (!Directory.Exists(directory) || PathContainsReparsePoint(directory))
        {
            Fail("recovery-directory-invalid");
        }
        string[] expectedTargets =
        [
            RequireAbsolute(request.CertificatePath),
            RequireAbsolute(request.PrivateKeyPath),
            RequireAbsolute(request.ProfilePath),
            RequireAbsolute(request.EnrollmentPath),
            RequireAbsolute(request.AuthorizationPolicyPath),
        ];
        if (expectedTargets.Distinct(PathComparer).Count() != 5)
        {
            Fail("recovery-targets-invalid");
        }

        string[] journalFiles = Directory.EnumerateFiles(
                directory,
                ".hase-python-provisioning-*.journal.json*")
            .ToArray();
        if (journalFiles.Length == 0)
        {
            return new PythonCredentialProvisioningRecoveryResult(
                PythonCredentialProvisioningRecoveryDisposition.NoTransaction,
                null);
        }

        string[] basePaths = journalFiles.Select(path =>
                path.EndsWith(".next", StringComparison.Ordinal)
                    ? path[..^5]
                    : path)
            .Distinct(PathComparer)
            .ToArray();
        if (basePaths.Length != 1)
        {
            Fail("recovery-ambiguous");
        }

        Journal? primary = File.Exists(basePaths[0])
            ? TryLoad(basePaths[0], expectedTargets)
            : null;
        Journal? next = File.Exists(basePaths[0] + ".next")
            ? TryLoad(basePaths[0] + ".next", expectedTargets)
            : null;
        Journal selected = Select(primary, next);
        if (!PathComparer.Equals(
            basePaths[0],
            Path.Combine(directory,
                $".hase-python-provisioning-{selected.TransactionId}.journal.json")))
        {
            Fail("recovery-journal-path-invalid");
        }

        Preflight(selected);
        if (selected.Phase == "committed")
        {
            CleanupCommitted(selected, basePaths[0]);
            return new PythonCredentialProvisioningRecoveryResult(
                PythonCredentialProvisioningRecoveryDisposition
                    .CommittedCleanupCompleted,
                selected.TransactionId);
        }

        RollBack(selected, basePaths[0]);
        return new PythonCredentialProvisioningRecoveryResult(
            PythonCredentialProvisioningRecoveryDisposition.RolledBack,
            selected.TransactionId);
    }

    private static Journal Select(Journal? primary, Journal? next)
    {
        if (primary is null && next is null)
        {
            Fail("recovery-journal-invalid");
        }
        if (primary is null)
        {
            return next!;
        }
        if (next is null)
        {
            return primary;
        }
        if (primary.TransactionId != next.TransactionId
            || primary.PlanId != next.PlanId)
        {
            Fail("recovery-ambiguous");
        }
        int primaryRank = PhaseRank(primary.Phase);
        int nextRank = PhaseRank(next.Phase);
        if (primaryRank == nextRank)
        {
            Fail("recovery-ambiguous");
        }
        return nextRank > primaryRank ? next : primary;
    }

    private static Journal? TryLoad(string path, string[] expectedTargets)
    {
        try
        {
            var file = new FileInfo(path);
            if (file.Length > MaximumJournalBytes || PathContainsReparsePoint(path))
            {
                return null;
            }
            byte[] bytes = File.ReadAllBytes(path);
            try
            {
                return Parse(bytes, expectedTargets);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidOperationException
            or FormatException
            or ArgumentException
            or PythonCredentialProvisioningRecoveryException)
        {
            return null;
        }
    }

    private static Journal Parse(ReadOnlySpan<byte> bytes, string[] targets)
    {
        var reader = new Utf8JsonReader(bytes);
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        if (reader.Read())
        {
            Fail("recovery-journal-invalid");
        }
        RejectDuplicates(document.RootElement);
        JsonElement root = document.RootElement;
        RequireProperties(root,
            "formatVersion", "transactionId", "planId", "phase",
            "sourceRevisions", "entries");
        if (root.GetProperty("formatVersion").GetInt32() != 1)
        {
            Fail("recovery-journal-invalid");
        }
        string id = RequireString(root.GetProperty("transactionId"));
        string planId = RequireString(root.GetProperty("planId"));
        string phase = RequireString(root.GetProperty("phase"));
        if (!TransactionIdPattern().IsMatch(id)
            || !planId.StartsWith("python-provisioning-plan-sha256:",
                StringComparison.Ordinal)
            || !HashPattern().IsMatch(planId[32..])
            || PhaseRank(phase) < 0)
        {
            Fail("recovery-journal-invalid");
        }
        JsonElement revisions = root.GetProperty("sourceRevisions");
        RequireProperties(revisions, "sourceProfileSha256", "enrollmentSha256",
            "authorizationPolicySha256");
        foreach (JsonProperty property in revisions.EnumerateObject())
        {
            if (!HashPattern().IsMatch(RequireString(property.Value)))
            {
                Fail("recovery-journal-invalid");
            }
        }

        JsonElement[] entries = root.GetProperty("entries").EnumerateArray().ToArray();
        if (entries.Length != 5)
        {
            Fail("recovery-journal-invalid");
        }
        var parsedEntries = new List<Entry>(5);
        for (int index = 0; index < 5; index++)
        {
            JsonElement entry = entries[index];
            RequireProperties(entry,
                "targetPath", "stagePath", "backupPath", "candidateSha256",
                "targetExisted", "originalSha256", "originalSecurity",
                "published", "backupCreated");
            string target = RequireString(entry.GetProperty("targetPath"));
            string stage = RequireString(entry.GetProperty("stagePath"));
            string backup = RequireString(entry.GetProperty("backupPath"));
            string candidateHash = RequireString(
                entry.GetProperty("candidateSha256"));
            bool existed = entry.GetProperty("targetExisted").GetBoolean();
            string? originalHash = OptionalString(
                entry.GetProperty("originalSha256"));
            string? security = OptionalString(entry.GetProperty("originalSecurity"));
            bool published = entry.GetProperty("published").GetBoolean();
            bool backupCreated = entry.GetProperty("backupCreated").GetBoolean();
            if (!PathComparer.Equals(target, targets[index])
                || !PathComparer.Equals(stage, target + ".stage-" + id)
                || !PathComparer.Equals(backup, target + ".backup-" + id)
                || !HashPattern().IsMatch(candidateHash)
                || (existed != (originalHash is not null))
                || (existed != (security is not null))
                || (originalHash is not null && !HashPattern().IsMatch(originalHash)))
            {
                Fail("recovery-journal-invalid");
            }
            parsedEntries.Add(new Entry(
                target, stage, backup, candidateHash, existed, originalHash,
                security, published, backupCreated));
        }
        int recordedPublishedCount = PublishedCountForPhase(phase);
        for (int index = 0; index < parsedEntries.Count; index++)
        {
            bool expectedPublished = index < recordedPublishedCount;
            bool expectedBackup = expectedPublished
                && parsedEntries[index].TargetExisted;
            if (parsedEntries[index].Published != expectedPublished
                || parsedEntries[index].BackupCreated != expectedBackup)
            {
                Fail("recovery-journal-invalid");
            }
        }
        return new Journal(id, planId, phase, parsedEntries);
    }

    private static void Preflight(Journal journal)
    {
        foreach (Entry entry in journal.Entries)
        {
            foreach (string path in new[]
                { entry.TargetPath, entry.StagePath, entry.BackupPath })
            {
                if (PathContainsReparsePoint(path))
                {
                    Fail("recovery-path-unsafe");
                }
            }
            string? targetHash = ExistingHash(entry.TargetPath);
            string? stageHash = ExistingHash(entry.StagePath);
            string? backupHash = ExistingHash(entry.BackupPath);
            if (stageHash is not null && stageHash != entry.CandidateSha256)
            {
                Fail("recovery-hash-mismatch");
            }
            if (backupHash is not null && backupHash != entry.OriginalSha256)
            {
                Fail("recovery-hash-mismatch");
            }
            if (journal.Phase == "committed")
            {
                if (targetHash != entry.CandidateSha256)
                {
                    Fail("recovery-committed-target-invalid");
                }
            }
            else if (entry.TargetExisted)
            {
                bool originalAtTarget = targetHash == entry.OriginalSha256;
                bool originalInBackup = backupHash == entry.OriginalSha256;
                if (originalAtTarget == originalInBackup
                    || (!originalAtTarget
                        && targetHash != entry.CandidateSha256))
                {
                    Fail("recovery-state-ambiguous");
                }
            }
            else if (targetHash is not null
                && targetHash != entry.CandidateSha256)
            {
                Fail("recovery-state-ambiguous");
            }
        }
    }

    private static void RollBack(Journal journal, string journalPath)
    {
        foreach (Entry entry in journal.Entries.AsEnumerable().Reverse())
        {
            string? targetHash = ExistingHash(entry.TargetPath);
            if (entry.TargetExisted)
            {
                if (ExistingHash(entry.BackupPath) == entry.OriginalSha256)
                {
                    if (targetHash is not null)
                    {
                        File.Delete(entry.TargetPath);
                    }
                    File.Move(entry.BackupPath, entry.TargetPath);
                    RestoreSecurity(entry.TargetPath, entry.OriginalSecurity);
                }
            }
            else if (targetHash == entry.CandidateSha256)
            {
                File.Delete(entry.TargetPath);
            }
        }
        CleanupArtifacts(journal, journalPath);
    }

    private static void CleanupCommitted(Journal journal, string journalPath) =>
        CleanupArtifacts(journal, journalPath);

    private static void CleanupArtifacts(Journal journal, string journalPath)
    {
        foreach (Entry entry in journal.Entries)
        {
            File.Delete(entry.StagePath);
            File.Delete(entry.BackupPath);
        }
        File.Delete(journalPath + ".next");
        File.Delete(journalPath);
    }

    private static string? ExistingHash(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        using FileStream stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        try
        {
            return Convert.ToHexStringLower(hash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash);
        }
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
                security, AccessControlSections.Access);
            new FileInfo(path).SetAccessControl(descriptor);
        }
        else
        {
            File.SetUnixFileMode(path, (UnixFileMode)int.Parse(security));
        }
    }

    private static int PhaseRank(string phase)
    {
        if (phase == "created") return 0;
        if (phase == "staged") return 1;
        if (phase == "committed") return 12;
        for (int index = 1; index <= 5; index++)
        {
            if (phase == $"publishing-{index}") return index * 2;
            if (phase == $"published-{index}") return index * 2 + 1;
        }
        return -1;
    }

    private static int PublishedCountForPhase(string phase)
    {
        if (phase is "created" or "staged") return 0;
        if (phase == "committed") return 5;
        if (phase.StartsWith("publishing-", StringComparison.Ordinal))
            return int.Parse(phase[11..]) - 1;
        if (phase.StartsWith("published-", StringComparison.Ordinal))
            return int.Parse(phase[10..]);
        Fail("recovery-journal-invalid");
        return 0;
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) Fail("recovery-journal-invalid");
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
                RejectDuplicates(item);
        }
    }

    private static void RequireProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.EnumerateObject().Select(p => p.Name)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(names.Order(StringComparer.Ordinal)))
            Fail("recovery-journal-invalid");
    }

    private static string RequireString(JsonElement element)
    {
        string? value = element.ValueKind == JsonValueKind.String
            ? element.GetString() : null;
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
            Fail("recovery-journal-invalid");
        return value;
    }

    private static string? OptionalString(JsonElement element) =>
        element.ValueKind == JsonValueKind.Null ? null : RequireString(element);

    private static string RequireAbsolute(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim()
            || !Path.IsPathFullyQualified(value))
            Fail("recovery-path-invalid");
        return Path.GetFullPath(value);
    }

    private static bool PathContainsReparsePoint(string path)
    {
        string? current = File.Exists(path) || Directory.Exists(path)
            ? path : Path.GetDirectoryName(path);
        while (current is not null)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
            current = Path.GetDirectoryName(current);
        }
        return false;
    }

    [DoesNotReturn]
    private static void Fail(string code) =>
        throw new PythonCredentialProvisioningRecoveryException(code);

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed record Journal(
        string TransactionId,
        string PlanId,
        string Phase,
        IReadOnlyList<Entry> Entries);

    private sealed record Entry(
        string TargetPath,
        string StagePath,
        string BackupPath,
        string CandidateSha256,
        bool TargetExisted,
        string? OriginalSha256,
        string? OriginalSecurity,
        bool Published,
        bool BackupCreated);
}
