using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonCredentialProvisioningRecovererTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(), $"hase-python-recovery-{Guid.NewGuid():N}");

    public PythonCredentialProvisioningRecovererTests() =>
        Directory.CreateDirectory(directory);

    [Fact]
    public void Recover_NoJournal_IsIdempotentNoTransaction()
    {
        PythonCredentialProvisioningRecoveryResult first =
            new PythonCredentialProvisioningRecoverer().Recover(Request());
        PythonCredentialProvisioningRecoveryResult second =
            new PythonCredentialProvisioningRecoverer().Recover(Request());

        Assert.Equal(
            PythonCredentialProvisioningRecoveryDisposition.NoTransaction,
            first.Disposition);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(0, "created")]
    [InlineData(0, "staged")]
    [InlineData(1, "published-1")]
    [InlineData(2, "published-2")]
    [InlineData(3, "published-3")]
    [InlineData(4, "published-4")]
    [InlineData(5, "published-5")]
    public void Recover_UncommittedInterruption_RestoresOriginalState(
        int publishedCount,
        string phase)
    {
        State state = CreateState(publishedCount, phase);

        PythonCredentialProvisioningRecoveryResult result =
            new PythonCredentialProvisioningRecoverer().Recover(state.Request);

        Assert.Equal(
            PythonCredentialProvisioningRecoveryDisposition.RolledBack,
            result.Disposition);
        Assert.False(File.Exists(state.Paths[0]));
        Assert.False(File.Exists(state.Paths[1]));
        Assert.False(File.Exists(state.Paths[2]));
        Assert.Equal(state.Originals[3], File.ReadAllBytes(state.Paths[3]));
        Assert.Equal(state.Originals[4], File.ReadAllBytes(state.Paths[4]));
        AssertNoArtifacts();
    }

    [Theory]
    [InlineData(0, "publishing-1")]
    [InlineData(1, "publishing-1")]
    [InlineData(1, "publishing-2")]
    [InlineData(2, "publishing-2")]
    [InlineData(2, "publishing-3")]
    [InlineData(3, "publishing-3")]
    [InlineData(3, "publishing-4")]
    [InlineData(4, "publishing-4")]
    [InlineData(4, "publishing-5")]
    [InlineData(5, "publishing-5")]
    public void Recover_InterruptedMoveBoundary_RestoresEitherPhysicalState(
        int physicallyPublishedCount,
        string phase)
    {
        State state = CreateState(physicallyPublishedCount, phase);

        PythonCredentialProvisioningRecoveryResult result =
            new PythonCredentialProvisioningRecoverer().Recover(state.Request);

        Assert.Equal(
            PythonCredentialProvisioningRecoveryDisposition.RolledBack,
            result.Disposition);
        Assert.False(File.Exists(state.Paths[0]));
        Assert.False(File.Exists(state.Paths[1]));
        Assert.False(File.Exists(state.Paths[2]));
        Assert.Equal(state.Originals[3], File.ReadAllBytes(state.Paths[3]));
        Assert.Equal(state.Originals[4], File.ReadAllBytes(state.Paths[4]));
        AssertNoArtifacts();
    }

    [Fact]
    public void Recover_CommittedInterruption_KeepsCandidatesAndCleansArtifacts()
    {
        State state = CreateState(5, "committed");

        PythonCredentialProvisioningRecoveryResult result =
            new PythonCredentialProvisioningRecoverer().Recover(state.Request);

        Assert.Equal(
            PythonCredentialProvisioningRecoveryDisposition
                .CommittedCleanupCompleted,
            result.Disposition);
        for (int index = 0; index < 5; index++)
        {
            Assert.Equal(state.Candidates[index],
                File.ReadAllBytes(state.Paths[index]));
        }
        AssertNoArtifacts();
    }

    [Fact]
    public void Recover_AdvancedNextJournal_SelectsCommittedState()
    {
        State state = CreateState(5, "published-5");
        WriteJournal(state, "committed", state.JournalPath + ".next");

        PythonCredentialProvisioningRecoveryResult result =
            new PythonCredentialProvisioningRecoverer().Recover(state.Request);

        Assert.Equal(
            PythonCredentialProvisioningRecoveryDisposition
                .CommittedCleanupCompleted,
            result.Disposition);
        AssertNoArtifacts();
    }

    [Fact]
    public void Recover_CorruptJournal_RejectsWithoutMutation()
    {
        State state = CreateState(2, "published-2");
        File.WriteAllText(state.JournalPath, "{ invalid");
        Dictionary<string, byte[]?> before = Snapshot(state);

        PythonCredentialProvisioningRecoveryException exception = Assert.Throws<
            PythonCredentialProvisioningRecoveryException>(() =>
                new PythonCredentialProvisioningRecoverer().Recover(state.Request));

        Assert.Equal("recovery-journal-invalid", exception.Code);
        AssertSnapshot(before);
    }

    [Fact]
    public void Recover_HashMismatch_RejectsWithoutMutation()
    {
        State state = CreateState(3, "published-3");
        File.WriteAllText(state.Paths[0], "tampered");
        Dictionary<string, byte[]?> before = Snapshot(state);

        PythonCredentialProvisioningRecoveryException exception = Assert.Throws<
            PythonCredentialProvisioningRecoveryException>(() =>
                new PythonCredentialProvisioningRecoverer().Recover(state.Request));

        Assert.Equal("recovery-state-ambiguous", exception.Code);
        AssertSnapshot(before);
    }

    [Fact]
    public void Recover_PathSubstitution_RejectsWithoutMutation()
    {
        State state = CreateState(1, "published-1");
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllBytes(state.JournalPath));
        string encodedOriginal = JsonSerializer.Serialize(state.Paths[0]);
        string encodedSubstitute = JsonSerializer.Serialize(
            Path.Combine(directory, "substituted.pem"));
        string text = document.RootElement.GetRawText().Replace(
            encodedOriginal,
            encodedSubstitute,
            StringComparison.Ordinal);
        File.WriteAllText(state.JournalPath, text);
        Dictionary<string, byte[]?> before = Snapshot(state);

        Assert.Throws<PythonCredentialProvisioningRecoveryException>(() =>
            new PythonCredentialProvisioningRecoverer().Recover(state.Request));
        AssertSnapshot(before);
    }

    [Fact]
    public void Recover_MultipleTransactions_RejectsWithoutMutation()
    {
        State state = CreateState(0, "staged");
        File.Copy(state.JournalPath, Path.Combine(
            directory,
            ".hase-python-provisioning-ffffffffffffffffffffffffffffffff.journal.json"));
        Dictionary<string, byte[]?> before = Snapshot(state);

        PythonCredentialProvisioningRecoveryException exception = Assert.Throws<
            PythonCredentialProvisioningRecoveryException>(() =>
                new PythonCredentialProvisioningRecoverer().Recover(state.Request));

        Assert.Equal("recovery-ambiguous", exception.Code);
        AssertSnapshot(before);
    }

    [Fact]
    public void Recover_EqualPrimaryAndNextStates_RejectsWithoutMutation()
    {
        State state = CreateState(2, "published-2");
        File.Copy(state.JournalPath, state.JournalPath + ".next");
        Dictionary<string, byte[]?> before = Snapshot(state);

        PythonCredentialProvisioningRecoveryException exception = Assert.Throws<
            PythonCredentialProvisioningRecoveryException>(() =>
                new PythonCredentialProvisioningRecoverer().Recover(state.Request));

        Assert.Equal("recovery-ambiguous", exception.Code);
        AssertSnapshot(before);
    }

    [Fact]
    public void Recover_MissingOriginalBackup_RejectsWithoutMutation()
    {
        State state = CreateState(4, "published-4");
        File.Delete(state.Paths[3] + ".backup-" + state.TransactionId);
        Dictionary<string, byte[]?> before = Snapshot(state);

        PythonCredentialProvisioningRecoveryException exception = Assert.Throws<
            PythonCredentialProvisioningRecoveryException>(() =>
                new PythonCredentialProvisioningRecoverer().Recover(state.Request));

        Assert.Equal("recovery-state-ambiguous", exception.Code);
        AssertSnapshot(before);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private State CreateState(int publishedCount, string phase)
    {
        const string id = "0123456789abcdef0123456789abcdef";
        string[] paths =
        [
            Path.Combine(directory, "python-client.pem"),
            Path.Combine(directory, "python-key.pem"),
            Path.Combine(directory, "python-profile.json"),
            Path.Combine(directory, "enrollment.json"),
            Path.Combine(directory, "authorization.json"),
        ];
        byte[][] originals = Enumerable.Range(0, 5)
            .Select(index => System.Text.Encoding.UTF8.GetBytes($"original-{index}"))
            .ToArray();
        byte[][] candidates = Enumerable.Range(0, 5)
            .Select(index => System.Text.Encoding.UTF8.GetBytes($"candidate-{index}"))
            .ToArray();
        for (int index = 3; index < 5; index++)
        {
            File.WriteAllBytes(paths[index], originals[index]);
        }
        string[] security =
        [null!, null!, null!, CaptureSecurity(paths[3]), CaptureSecurity(paths[4])];
        for (int index = 0; index < 5; index++)
        {
            string stage = paths[index] + ".stage-" + id;
            string backup = paths[index] + ".backup-" + id;
            if (index < publishedCount)
            {
                if (index >= 3)
                {
                    File.Move(paths[index], backup);
                }
                File.WriteAllBytes(paths[index], candidates[index]);
            }
            else
            {
                File.WriteAllBytes(stage, candidates[index]);
            }
        }
        string journal = Path.Combine(directory,
            $".hase-python-provisioning-{id}.journal.json");
        var state = new State(
            Request(paths), paths, originals, candidates, security, id, journal);
        WriteJournal(state, phase, journal);
        return state;
    }

    private static void WriteJournal(State state, string phase, string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            transactionId = state.TransactionId,
            planId = "python-provisioning-plan-sha256:" + new string('a', 64),
            phase,
            sourceRevisions = new
            {
                sourceProfileSha256 = new string('b', 64),
                enrollmentSha256 = new string('c', 64),
                authorizationPolicySha256 = new string('d', 64),
            },
            entries = Enumerable.Range(0, 5).Select(index => new
            {
                targetPath = state.Paths[index],
                stagePath = state.Paths[index] + ".stage-" + state.TransactionId,
                backupPath = state.Paths[index] + ".backup-" + state.TransactionId,
                candidateSha256 = Hash(state.Candidates[index]),
                targetExisted = index >= 3,
                originalSha256 = index >= 3 ? Hash(state.Originals[index]) : null,
                originalSecurity = index >= 3 ? state.Security[index] : null,
                published = index < PublishedCount(phase),
                backupCreated = index >= 3 && index < PublishedCount(phase),
            }),
        }));
    }

    private PythonCredentialProvisioningRecoveryRequest Request() => Request(
    [
        Path.Combine(directory, "python-client.pem"),
        Path.Combine(directory, "python-key.pem"),
        Path.Combine(directory, "python-profile.json"),
        Path.Combine(directory, "enrollment.json"),
        Path.Combine(directory, "authorization.json"),
    ]);

    private PythonCredentialProvisioningRecoveryRequest Request(string[] paths) =>
        new(directory, paths[0], paths[1], paths[2], paths[3], paths[4]);

    private static int PublishedCount(string phase)
    {
        if (phase == "committed") return 5;
        if (phase.StartsWith("publishing-", StringComparison.Ordinal))
            return int.Parse(phase[11..]) - 1;
        if (phase.StartsWith("published-", StringComparison.Ordinal))
            return int.Parse(phase[10..]);
        return 0;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(
        SHA256.HashData(bytes));

    private static string CaptureSecurity(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return new FileInfo(path).GetAccessControl()
                .GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        }
        return Convert.ToInt32(File.GetUnixFileMode(path)).ToString();
    }

    private Dictionary<string, byte[]?> Snapshot(State state)
    {
        return Directory.EnumerateFiles(directory).ToDictionary(
            path => path,
            path => (byte[]?)File.ReadAllBytes(path),
            PathComparer);
    }

    private void AssertSnapshot(Dictionary<string, byte[]?> expected)
    {
        Dictionary<string, byte[]?> actual = Directory.EnumerateFiles(directory)
            .ToDictionary(path => path, path => (byte[]?)File.ReadAllBytes(path),
                PathComparer);
        Assert.Equal(
            expected.Keys.OrderBy(path => path, PathComparer),
            actual.Keys.OrderBy(path => path, PathComparer));
        foreach (string path in expected.Keys)
            Assert.Equal(expected[path], actual[path]);
    }

    private void AssertNoArtifacts()
    {
        Assert.DoesNotContain(Directory.EnumerateFiles(directory), path =>
            path.Contains(".stage-", StringComparison.Ordinal)
            || path.Contains(".backup-", StringComparison.Ordinal)
            || path.Contains(".journal.json", StringComparison.Ordinal));
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed record State(
        PythonCredentialProvisioningRecoveryRequest Request,
        string[] Paths,
        byte[][] Originals,
        byte[][] Candidates,
        string[] Security,
        string TransactionId,
        string JournalPath);
}
