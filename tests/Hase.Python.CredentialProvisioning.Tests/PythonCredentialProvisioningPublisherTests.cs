using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonCredentialProvisioningPublisherTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(
        2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(
        Path.GetTempPath(), $"hase-python-publisher-{Guid.NewGuid():N}");

    public PythonCredentialProvisioningPublisherTests() =>
        Directory.CreateDirectory(directory);

    [Fact]
    public async Task PublishAsync_ValidatedCandidates_PublishesInAuthoritySafeOrder()
    {
        using Fixture fixture = await CreateFixtureAsync();
        byte[] expectedCertificate = fixture.Candidates.CertificatePem.ToArray();
        byte[] expectedKey = fixture.Candidates.PrivateKeyPem.ToArray();
        byte[] expectedProfile = fixture.Candidates.ProfileDocument.ToArray();
        byte[] expectedEnrollment = fixture.Candidates.EnrollmentDocument.ToArray();
        byte[] expectedPolicy = fixture.Candidates.AuthorizationPolicyDocument.ToArray();

        PythonCredentialProvisioningPublicationResult result =
            await PublishAsync(fixture, new PythonCredentialProvisioningPublisher());

        Assert.False(result.ReplacedCredentialOutputs);
        Assert.Equal(expectedCertificate, File.ReadAllBytes(fixture.Plan.CertificatePath));
        Assert.Equal(expectedKey, File.ReadAllBytes(fixture.Plan.PrivateKeyPath));
        Assert.Equal(expectedProfile, File.ReadAllBytes(fixture.Plan.ProfilePath));
        Assert.Equal(expectedEnrollment, File.ReadAllBytes(fixture.Plan.EnrollmentPath));
        Assert.Equal(expectedPolicy, File.ReadAllBytes(fixture.Plan.AuthorizationPolicyPath));
        AssertRestricted(fixture.Plan.PrivateKeyPath);
        AssertNoTransactionArtifacts();
        CryptographicOperations.ZeroMemory(expectedCertificate);
        CryptographicOperations.ZeroMemory(expectedKey);
        CryptographicOperations.ZeroMemory(expectedProfile);
        CryptographicOperations.ZeroMemory(expectedEnrollment);
        CryptographicOperations.ZeroMemory(expectedPolicy);
    }

    [Theory]
    [InlineData((int)PythonCredentialPublicationStep.Staged)]
    [InlineData((int)PythonCredentialPublicationStep.JournalDurable)]
    [InlineData((int)PythonCredentialPublicationStep.CertificatePublished)]
    [InlineData((int)PythonCredentialPublicationStep.PrivateKeyPublished)]
    [InlineData((int)PythonCredentialPublicationStep.ProfilePublished)]
    [InlineData((int)PythonCredentialPublicationStep.EnrollmentPublished)]
    [InlineData((int)PythonCredentialPublicationStep.AuthorizationPolicyPublished)]
    public async Task PublishAsync_FailureAtEveryBoundary_RestoresExactState(
        int failureStepValue)
    {
        var failureStep = (PythonCredentialPublicationStep)failureStepValue;
        using Fixture fixture = await CreateFixtureAsync();
        byte[] originalEnrollment = File.ReadAllBytes(fixture.Plan.EnrollmentPath);
        byte[] originalPolicy = File.ReadAllBytes(fixture.Plan.AuthorizationPolicyPath);
        string enrollmentSecurity = SecurityDescription(fixture.Plan.EnrollmentPath);
        string policySecurity = SecurityDescription(fixture.Plan.AuthorizationPolicyPath);
        var publisher = new PythonCredentialProvisioningPublisher(step =>
        {
            if (step == failureStep)
            {
                throw new InvalidOperationException("injected");
            }
        });

        PythonCredentialProvisioningPublicationException exception =
            await Assert.ThrowsAsync<PythonCredentialProvisioningPublicationException>(
                () => PublishAsync(fixture, publisher));

        Assert.Equal("transaction-failed", exception.Code);
        Assert.False(File.Exists(fixture.Plan.CertificatePath));
        Assert.False(File.Exists(fixture.Plan.PrivateKeyPath));
        Assert.False(File.Exists(fixture.Plan.ProfilePath));
        Assert.Equal(originalEnrollment, File.ReadAllBytes(fixture.Plan.EnrollmentPath));
        Assert.Equal(originalPolicy, File.ReadAllBytes(fixture.Plan.AuthorizationPolicyPath));
        Assert.Equal(enrollmentSecurity,
            SecurityDescription(fixture.Plan.EnrollmentPath));
        Assert.Equal(policySecurity,
            SecurityDescription(fixture.Plan.AuthorizationPolicyPath));
        AssertNoTransactionArtifacts();
        CryptographicOperations.ZeroMemory(originalEnrollment);
        CryptographicOperations.ZeroMemory(originalPolicy);
    }

    [Fact]
    public async Task PublishAsync_JournalContainsMetadataButNoCredentialContent()
    {
        using Fixture fixture = await CreateFixtureAsync();
        bool inspected = false;
        var publisher = new PythonCredentialProvisioningPublisher(step =>
        {
            if (step != PythonCredentialPublicationStep.JournalDurable)
            {
                return;
            }
            string journal = Assert.Single(Directory.EnumerateFiles(
                directory, "*.journal.json"));
            string contents = File.ReadAllText(journal);
            Assert.Contains(fixture.Plan.PlanId, contents, StringComparison.Ordinal);
            Assert.Contains("candidateSha256", contents, StringComparison.Ordinal);
            Assert.Contains("sourceRevisions", contents, StringComparison.Ordinal);
            Assert.Contains("originalSha256", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("BEGIN PRIVATE KEY", contents, StringComparison.Ordinal);
            inspected = true;
            throw new InvalidOperationException("inspection complete");
        });

        await Assert.ThrowsAsync<PythonCredentialProvisioningPublicationException>(
            () => PublishAsync(fixture, publisher));

        Assert.True(inspected);
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task PublishAsync_PublicationStepsExposeAuthoritySafeOrder()
    {
        using Fixture fixture = await CreateFixtureAsync();
        byte[] originalEnrollment = File.ReadAllBytes(fixture.Plan.EnrollmentPath);
        byte[] originalPolicy = File.ReadAllBytes(fixture.Plan.AuthorizationPolicyPath);
        var observed = new List<PythonCredentialPublicationStep>();
        var publisher = new PythonCredentialProvisioningPublisher(step =>
        {
            observed.Add(step);
            if ((int)step
                < (int)PythonCredentialPublicationStep.CertificatePublished)
            {
                return;
            }
            Assert.Equal(
                (int)step
                    >= (int)PythonCredentialPublicationStep.CertificatePublished,
                File.Exists(fixture.Plan.CertificatePath));
            Assert.Equal(
                (int)step
                    >= (int)PythonCredentialPublicationStep.PrivateKeyPublished,
                File.Exists(fixture.Plan.PrivateKeyPath));
            Assert.Equal(
                (int)step >= (int)PythonCredentialPublicationStep.ProfilePublished,
                File.Exists(fixture.Plan.ProfilePath));
            Assert.Equal(
                (int)step >= (int)PythonCredentialPublicationStep.EnrollmentPublished
                    ? fixture.Candidates.EnrollmentDocument.ToArray()
                    : originalEnrollment,
                File.ReadAllBytes(fixture.Plan.EnrollmentPath));
            Assert.Equal(
                (int)step
                    >= (int)PythonCredentialPublicationStep.AuthorizationPolicyPublished
                    ? fixture.Candidates.AuthorizationPolicyDocument.ToArray()
                    : originalPolicy,
                File.ReadAllBytes(fixture.Plan.AuthorizationPolicyPath));
        });

        await PublishAsync(fixture, publisher);

        Assert.Equal(
        new[]
        {
            PythonCredentialPublicationStep.JournalDurable,
            PythonCredentialPublicationStep.Staged,
            PythonCredentialPublicationStep.CertificatePublished,
            PythonCredentialPublicationStep.PrivateKeyPublished,
            PythonCredentialPublicationStep.ProfilePublished,
            PythonCredentialPublicationStep.EnrollmentPublished,
            PythonCredentialPublicationStep.AuthorizationPolicyPublished,
        }, observed);
        CryptographicOperations.ZeroMemory(originalEnrollment);
        CryptographicOperations.ZeroMemory(originalPolicy);
    }

    [Fact]
    public async Task PublishAsync_ReplacementAuthorized_ReplacesThreeOutputs()
    {
        using Fixture fixture = await CreateFixtureAsync(allowReplacement: true);

        PythonCredentialProvisioningPublicationResult result =
            await PublishAsync(fixture, new PythonCredentialProvisioningPublisher());

        Assert.True(result.ReplacedCredentialOutputs);
        Assert.Equal(fixture.Candidates.CertificatePem.ToArray(),
            File.ReadAllBytes(fixture.Plan.CertificatePath));
        Assert.Equal(fixture.Candidates.PrivateKeyPem.ToArray(),
            File.ReadAllBytes(fixture.Plan.PrivateKeyPath));
        Assert.Equal(fixture.Candidates.ProfileDocument.ToArray(),
            File.ReadAllBytes(fixture.Plan.ProfilePath));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task PublishAsync_ReplacementFailure_RestoresAllFiveOriginals()
    {
        using Fixture fixture = await CreateFixtureAsync(allowReplacement: true);
        Dictionary<string, byte[]> originals = FivePaths(fixture.Plan)
            .ToDictionary(path => path, File.ReadAllBytes);
        var publisher = new PythonCredentialProvisioningPublisher(step =>
        {
            if (step == PythonCredentialPublicationStep.AuthorizationPolicyPublished)
            {
                throw new InvalidOperationException("injected");
            }
        });

        await Assert.ThrowsAsync<PythonCredentialProvisioningPublicationException>(
            () => PublishAsync(fixture, publisher));

        foreach ((string path, byte[] contents) in originals)
        {
            Assert.Equal(contents, File.ReadAllBytes(path));
            CryptographicOperations.ZeroMemory(contents);
        }
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task PublishAsync_ChangedInput_RejectsBeforeStaging()
    {
        using Fixture fixture = await CreateFixtureAsync();
        File.AppendAllText(fixture.Plan.AuthorizationPolicyPath, " ");

        PythonCredentialProvisioningPublicationException exception =
            await Assert.ThrowsAsync<PythonCredentialProvisioningPublicationException>(
                () => PublishAsync(fixture, new PythonCredentialProvisioningPublisher()));

        Assert.Equal("input-revision-mismatch", exception.Code);
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task PublishAsync_RetainedJournal_RejectsBeforeStaging()
    {
        using Fixture fixture = await CreateFixtureAsync();
        string retained = Path.Combine(
            directory,
            ".hase-python-provisioning-retained.journal.json");
        File.WriteAllText(retained, "retained");

        PythonCredentialProvisioningPublicationException exception =
            await Assert.ThrowsAsync<PythonCredentialProvisioningPublicationException>(
                () => PublishAsync(fixture, new PythonCredentialProvisioningPublisher()));

        Assert.Equal("publication-target-invalid", exception.Code);
        Assert.False(File.Exists(fixture.Plan.CertificatePath));
        Assert.False(File.Exists(fixture.Plan.PrivateKeyPath));
        Assert.False(File.Exists(fixture.Plan.ProfilePath));
        File.Delete(retained);
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task PublishAsync_AlteredReplacementAuthorization_Rejects()
    {
        using Fixture fixture = await CreateFixtureAsync();
        PythonCredentialProvisioningPlan altered = fixture.Plan with
        {
            AllowReplacement = true,
        };

        PythonCredentialProvisioningPublicationException exception =
            await Assert.ThrowsAsync<PythonCredentialProvisioningPublicationException>(() =>
                new PythonCredentialProvisioningPublisher().PublishAsync(
                    altered,
                    fixture.Candidates,
                    [fixture.Root],
                    [fixture.Root],
                    Now));

        Assert.Equal("plan-revision-invalid", exception.Code);
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task PublishAsync_TargetChangesAfterStaging_RollsBackPublishedOutputs()
    {
        using Fixture fixture = await CreateFixtureAsync();
        byte[] changedEnrollment = Encoding.UTF8.GetBytes("externally changed");
        var publisher = new PythonCredentialProvisioningPublisher(step =>
        {
            if (step == PythonCredentialPublicationStep.Staged)
            {
                File.WriteAllBytes(fixture.Plan.EnrollmentPath, changedEnrollment);
            }
        });

        PythonCredentialProvisioningPublicationException exception =
            await Assert.ThrowsAsync<PythonCredentialProvisioningPublicationException>(
                () => PublishAsync(fixture, publisher));

        Assert.Equal("target-revision-mismatch", exception.Code);
        Assert.False(File.Exists(fixture.Plan.CertificatePath));
        Assert.False(File.Exists(fixture.Plan.PrivateKeyPath));
        Assert.False(File.Exists(fixture.Plan.ProfilePath));
        Assert.Equal(changedEnrollment,
            File.ReadAllBytes(fixture.Plan.EnrollmentPath));
        AssertNoTransactionArtifacts();
        CryptographicOperations.ZeroMemory(changedEnrollment);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private async Task<Fixture> CreateFixtureAsync(bool allowReplacement = false)
    {
        X509Certificate2 root = CreateRoot();
        PythonClientCredentialMaterial material = PythonClientCredentialFactory.Create(
            root, Now, TimeSpan.FromDays(30));
        string trusted = Write("trusted.pem", "trusted");
        string certificatePath = Path.Combine(directory, "python-client.pem");
        string privateKeyPath = Path.Combine(directory, "python-key.pem");
        string profilePath = Path.Combine(directory, "python-profile.json");
        string sourceProfile = Path.Combine(directory, "source-profile.json");
        File.WriteAllText(sourceProfile, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            address = "https://192.0.2.10:50443",
            clientCertificate = new
            {
                certificateChainPath = certificatePath,
                privateKeyPath,
            },
            trustedServerCertificate = new { certificatePath = trusted },
        }));
        string enrollment = Path.Combine(directory, "enrollment.json");
        File.WriteAllText(enrollment, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            enrollments = new[]
            {
                new
                {
                    credentialId = "x509-sha256:" + new string('b', 64),
                    principalId = "existing-client",
                    trustPolicyId = "private-network-validation-v1",
                },
            },
        }));
        string policy = Path.Combine(directory, "authorization.json");
        File.WriteAllText(policy, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            grants = new[]
            {
                new
                {
                    principalId = "existing-client",
                    permission = "runtime-host.snapshot.read",
                },
            },
        }));
        if (allowReplacement)
        {
            File.WriteAllText(certificatePath, "old output certificate");
            File.WriteAllText(privateKeyPath, "old output key");
            File.WriteAllText(profilePath, "old output profile");
        }
        var request = new PythonCredentialProvisioningPlanRequest(
            root.Thumbprint,
            material.CredentialId,
            "hase-python-automation",
            "private-network-validation-v1",
            sourceProfile,
            directory,
            certificatePath,
            privateKeyPath,
            profilePath,
            enrollment,
            policy,
            HashFile(policy),
            TimeSpan.FromDays(30),
            allowReplacement);
        PythonCredentialProvisioningPlan plan =
            await new PythonCredentialProvisioningPlanBuilder().CreateAsync(
                request, Now, [root], [root]);
        PythonCredentialProvisioningCandidates candidates =
            await new PythonCredentialProvisioningPreparer().PrepareAsync(
                plan, material, [root], [root]);
        return new Fixture(root, material, plan, candidates);
    }

    private Task<PythonCredentialProvisioningPublicationResult> PublishAsync(
        Fixture fixture,
        PythonCredentialProvisioningPublisher publisher) =>
        publisher.PublishAsync(
            fixture.Plan,
            fixture.Candidates,
            [fixture.Root],
            [fixture.Root],
            Now);

    private string Write(string name, string contents)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, contents);
        return path;
    }

    private static string HashFile(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    private static X509Certificate2 CreateRoot()
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=HASE Python Publication Test Root",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        return request.CreateSelfSigned(Now.AddYears(-1), Now.AddYears(1));
    }

    private void AssertNoTransactionArtifacts()
    {
        Assert.DoesNotContain(Directory.EnumerateFiles(directory), path =>
            path.Contains(".stage-", StringComparison.Ordinal)
            || path.Contains(".backup-", StringComparison.Ordinal)
            || path.Contains(".journal.json", StringComparison.Ordinal));
    }

    private static IEnumerable<string> FivePaths(
        PythonCredentialProvisioningPlan plan)
    {
        yield return plan.CertificatePath;
        yield return plan.PrivateKeyPath;
        yield return plan.ProfilePath;
        yield return plan.EnrollmentPath;
        yield return plan.AuthorizationPolicyPath;
    }

    private static void AssertRestricted(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            FileSecurity security = new FileInfo(path).GetAccessControl();
            Assert.True(security.AreAccessRulesProtected);
            SecurityIdentifier owner = Assert.IsType<SecurityIdentifier>(
                security.GetOwner(typeof(SecurityIdentifier)));
            Assert.Equal(WindowsIdentity.GetCurrent().User, owner);
            AuthorizationRuleCollection rules = security.GetAccessRules(
                true, false, typeof(SecurityIdentifier));
            FileSystemAccessRule rule = Assert.Single(
                rules.Cast<FileSystemAccessRule>());
            Assert.Equal(owner, rule.IdentityReference);
            Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        }
        else
        {
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(path));
        }
    }

    private static string SecurityDescription(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            FileSecurity security = new FileInfo(path).GetAccessControl();
            IEnumerable<string> rules = security.GetAccessRules(
                    includeExplicit: true,
                    includeInherited: true,
                    targetType: typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Select(rule => string.Join(
                    ":",
                    rule.IdentityReference.Value,
                    rule.AccessControlType,
                    rule.FileSystemRights,
                    rule.InheritanceFlags,
                    rule.PropagationFlags,
                    rule.IsInherited))
                .Order(StringComparer.Ordinal);
            return security.AreAccessRulesProtected
                + "|"
                + string.Join("|", rules);
        }
        return Convert.ToInt32(File.GetUnixFileMode(path)).ToString();
    }

    private sealed record Fixture(
        X509Certificate2 Root,
        PythonClientCredentialMaterial Material,
        PythonCredentialProvisioningPlan Plan,
        PythonCredentialProvisioningCandidates Candidates) : IDisposable
    {
        public void Dispose()
        {
            Candidates.Dispose();
            Material.Dispose();
            Root.Dispose();
        }
    }
}
