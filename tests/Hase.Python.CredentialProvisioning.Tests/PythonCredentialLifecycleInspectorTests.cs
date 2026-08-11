using Hase.Python.CredentialProvisioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonCredentialLifecycleInspectorTests : IDisposable
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 18, 0, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(
        Path.GetTempPath(), $"hase-python-lifecycle-{Guid.NewGuid():N}");

    public PythonCredentialLifecycleInspectorTests() =>
        Directory.CreateDirectory(directory);

    [Fact]
    public async Task InspectAsync_ExactSelectedState_ReturnsRotationEvidence()
    {
        using Fixture fixture = CreateFixture(TimeSpan.FromDays(30));

        PythonCredentialLifecycleInspectionResult result =
            await new PythonCredentialLifecycleInspector().InspectAsync(
                fixture.Request, Now);

        Assert.Equal(PythonCredentialLifecycleState.RotationDue, result.State);
        Assert.Equal(fixture.Material.CredentialId, result.CredentialId);
        Assert.Equal("hase-laptop-python-minipc", result.PrincipalId);
        Assert.Equal("private-network-validation-v1", result.TrustPolicyId);
        Assert.Equal(29, result.RemainingWholeDays);
        Assert.Equal(fixture.ExpectedGrants.Order(StringComparer.Ordinal),
            result.AuthorizationGrants);
        Assert.All(new[]
        {
            result.ProfileSha256,
            result.EnrollmentSha256,
            result.AuthorizationPolicySha256,
            result.TrustedServerCertificateSha256,
        }, value => Assert.Matches("^[0-9a-f]{64}$", value));
    }

    [Fact]
    public async Task InspectAsync_WidenedAuthorization_Rejects()
    {
        using Fixture fixture = CreateFixture(TimeSpan.FromDays(60),
            includeUnexpectedGrant: true);

        PythonCredentialLifecycleInspectionException exception =
            await Assert.ThrowsAsync<PythonCredentialLifecycleInspectionException>(
                () => new PythonCredentialLifecycleInspector().InspectAsync(
                    fixture.Request, Now));

        Assert.Equal("authorization-grants-mismatch", exception.Code);
    }

    [Fact]
    public async Task InspectAsync_NonPairingPrivateKey_RejectsSanitized()
    {
        using Fixture fixture = CreateFixture(TimeSpan.FromDays(60));
        using PythonClientCredentialMaterial other =
            PythonClientCredentialFactory.Create(
                fixture.Root, Now, TimeSpan.FromDays(60));
        File.WriteAllBytes(fixture.PrivateKeyPath, other.PrivateKeyPem.ToArray());

        PythonCredentialLifecycleInspectionException exception =
            await Assert.ThrowsAsync<PythonCredentialLifecycleInspectionException>(
                () => new PythonCredentialLifecycleInspector().InspectAsync(
                    fixture.Request, Now));

        Assert.Equal("credential-material-invalid", exception.Code);
        Assert.DoesNotContain(fixture.PrivateKeyPath, exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RotationPreparation_ExactSource_CreatesOverlapAndFinalCandidates()
    {
        using Fixture fixture = CreateFixture(TimeSpan.FromDays(60));
        PythonCredentialLifecycleInspectionResult inspected =
            await InspectAsync(fixture);
        using PythonClientCredentialMaterial replacement =
            PythonClientCredentialFactory.Create(
                fixture.Root, Now, TimeSpan.FromDays(90));
        using PythonCredentialRotationCandidates candidates =
            await new PythonCredentialRotationPreparer().PrepareAsync(
                RotationRequest(fixture, inspected), replacement, Now);

        Assert.Equal(fixture.Material.CredentialId,
            candidates.CurrentCredentialId);
        Assert.Equal(replacement.CredentialId,
            candidates.ReplacementCredentialId);
        Assert.NotEqual(candidates.CurrentCredentialId,
            candidates.ReplacementCredentialId);
        Assert.Equal(File.ReadAllBytes(fixture.Request.ProfilePath),
            candidates.ProfileDocument.ToArray());
        Assert.Equal(File.ReadAllBytes(fixture.Request.AuthorizationPolicyPath),
            candidates.AuthorizationPolicyDocument.ToArray());

        AssertEnrollment(candidates.OverlapEnrollmentDocument.Span,
            fixture.Material.CredentialId, expected: true);
        AssertEnrollment(candidates.OverlapEnrollmentDocument.Span,
            replacement.CredentialId, expected: true);
        AssertEnrollment(candidates.FinalEnrollmentDocument.Span,
            fixture.Material.CredentialId, expected: false);
        AssertEnrollment(candidates.FinalEnrollmentDocument.Span,
            replacement.CredentialId, expected: true);
    }

    [Fact]
    public async Task RotationPreparation_ChangedSourceRevision_Rejects()
    {
        using Fixture fixture = CreateFixture(TimeSpan.FromDays(60));
        PythonCredentialLifecycleInspectionResult inspected =
            await InspectAsync(fixture);
        using PythonClientCredentialMaterial replacement =
            PythonClientCredentialFactory.Create(
                fixture.Root, Now, TimeSpan.FromDays(90));
        File.AppendAllText(fixture.Request.AuthorizationPolicyPath, " ");

        PythonCredentialRotationPreparationException exception =
            await Assert.ThrowsAsync<PythonCredentialRotationPreparationException>(
                () => new PythonCredentialRotationPreparer().PrepareAsync(
                    RotationRequest(fixture, inspected), replacement, Now));

        Assert.Equal("rotation-source-revision-mismatch", exception.Code);
    }

    [Fact]
    public async Task RotationCandidates_DisposeZerosEveryBuffer()
    {
        using Fixture fixture = CreateFixture(TimeSpan.FromDays(60));
        PythonCredentialLifecycleInspectionResult inspected =
            await InspectAsync(fixture);
        using PythonClientCredentialMaterial replacement =
            PythonClientCredentialFactory.Create(
                fixture.Root, Now, TimeSpan.FromDays(90));
        PythonCredentialRotationCandidates candidates =
            await new PythonCredentialRotationPreparer().PrepareAsync(
                RotationRequest(fixture, inspected), replacement, Now);
        ReadOnlyMemory<byte>[] buffers =
        [
            candidates.ReplacementCertificatePem,
            candidates.ReplacementPrivateKeyPem,
            candidates.ProfileDocument,
            candidates.OverlapEnrollmentDocument,
            candidates.FinalEnrollmentDocument,
            candidates.AuthorizationPolicyDocument,
        ];

        candidates.Dispose();
        candidates.Dispose();

        foreach (ReadOnlyMemory<byte> buffer in buffers)
            Assert.All(buffer.ToArray(), value => Assert.Equal(0, value));
        Assert.Throws<ObjectDisposedException>(
            () => candidates.ReplacementPrivateKeyPem);
    }

    [Fact]
    public async Task RotationPublication_BeginThenRecover_RestoresExactSources()
    {
        using Fixture fixture = CreateFixture(TimeSpan.FromDays(60));
        PythonCredentialLifecycleInspectionResult inspected =
            await InspectAsync(fixture);
        using PythonClientCredentialMaterial replacement =
            PythonClientCredentialFactory.Create(
                fixture.Root, Now, TimeSpan.FromDays(90));
        using PythonCredentialRotationCandidates candidates =
            await new PythonCredentialRotationPreparer().PrepareAsync(
                RotationRequest(fixture, inspected), replacement, Now);
        PythonCredentialRotationPublicationRequest publication =
            PublicationRequest(fixture, inspected);

        PythonCredentialRotationPublicationResult begun =
            await new PythonCredentialRotationPublisher().BeginAsync(
                publication, candidates);
        Assert.Equal("OverlapPublished", begun.Disposition);
        Assert.True(begun.RollbackRetained);
        Assert.Equal(replacement.CertificatePem.ToArray(),
            File.ReadAllBytes(fixture.CertificatePath));
        AssertEnrollment(File.ReadAllBytes(fixture.Request.EnrollmentPath),
            fixture.Material.CredentialId, expected: true);
        AssertEnrollment(File.ReadAllBytes(fixture.Request.EnrollmentPath),
            replacement.CredentialId, expected: true);

        PythonCredentialRotationPublicationResult recovered =
            new PythonCredentialRotationPublisher().Recover(publication);

        Assert.Equal("RolledBack", recovered.Disposition);
        Assert.Equal(inspected.ClientCertificateSha256,
            HashFile(fixture.CertificatePath));
        Assert.Equal(inspected.ClientPrivateKeySha256,
            HashFile(fixture.PrivateKeyPath));
        Assert.Equal(inspected.EnrollmentSha256,
            HashFile(fixture.Request.EnrollmentPath));
        AssertNoRotationArtifacts();
    }

    [Fact]
    public async Task RotationPublication_BeginThenFinalize_RevokesOldAndCleans()
    {
        using Fixture fixture = CreateFixture(TimeSpan.FromDays(60));
        PythonCredentialLifecycleInspectionResult inspected =
            await InspectAsync(fixture);
        using PythonClientCredentialMaterial replacement =
            PythonClientCredentialFactory.Create(
                fixture.Root, Now, TimeSpan.FromDays(90));
        using PythonCredentialRotationCandidates candidates =
            await new PythonCredentialRotationPreparer().PrepareAsync(
                RotationRequest(fixture, inspected), replacement, Now);
        PythonCredentialRotationPublicationRequest publication =
            PublicationRequest(fixture, inspected);

        _ = await new PythonCredentialRotationPublisher().BeginAsync(
            publication, candidates);
        PythonCredentialRotationPublicationResult finalized =
            new PythonCredentialRotationPublisher().Finalize(publication);

        Assert.Equal("Finalized", finalized.Disposition);
        Assert.False(finalized.RollbackRetained);
        AssertEnrollment(File.ReadAllBytes(fixture.Request.EnrollmentPath),
            fixture.Material.CredentialId, expected: false);
        AssertEnrollment(File.ReadAllBytes(fixture.Request.EnrollmentPath),
            replacement.CredentialId, expected: true);
        Assert.Equal(inspected.AuthorizationPolicySha256,
            HashFile(fixture.Request.AuthorizationPolicyPath));
        AssertNoRotationArtifacts();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task RotationPublication_InjectedFailure_RestoresExactSources(
        int failureStepValue)
    {
        PythonCredentialRotationPublicationStep failureStep =
            (PythonCredentialRotationPublicationStep)failureStepValue;
        using Fixture fixture = CreateFixture(TimeSpan.FromDays(60));
        PythonCredentialLifecycleInspectionResult inspected =
            await InspectAsync(fixture);
        using PythonClientCredentialMaterial replacement =
            PythonClientCredentialFactory.Create(
                fixture.Root, Now, TimeSpan.FromDays(90));
        using PythonCredentialRotationCandidates candidates =
            await new PythonCredentialRotationPreparer().PrepareAsync(
                RotationRequest(fixture, inspected), replacement, Now);

        PythonCredentialRotationPublicationException exception =
            await Assert.ThrowsAsync<PythonCredentialRotationPublicationException>(
                () => new PythonCredentialRotationPublisher(step =>
                {
                    if (step == failureStep) throw new InvalidOperationException();
                }).BeginAsync(PublicationRequest(fixture, inspected), candidates));

        Assert.Equal("rotation-publication-failed", exception.Code);
        Assert.Equal(inspected.ClientCertificateSha256,
            HashFile(fixture.CertificatePath));
        Assert.Equal(inspected.ClientPrivateKeySha256,
            HashFile(fixture.PrivateKeyPath));
        Assert.Equal(inspected.ProfileSha256,
            HashFile(fixture.Request.ProfilePath));
        Assert.Equal(inspected.EnrollmentSha256,
            HashFile(fixture.Request.EnrollmentPath));
        Assert.Equal(inspected.AuthorizationPolicySha256,
            HashFile(fixture.Request.AuthorizationPolicyPath));
        AssertNoRotationArtifacts();
    }

    [Fact]
    public async Task RotationOrchestrator_BeginRecover_ComposesExplicitBoundaries()
    {
        using Fixture fixture = CreateFixture(TimeSpan.FromDays(60));
        PythonCredentialLifecycleInspectionResult inspected =
            await InspectAsync(fixture);
        using PythonClientCredentialMaterial replacement =
            PythonClientCredentialFactory.Create(
                fixture.Root, Now, TimeSpan.FromDays(90));
        var orchestrator = new PythonCredentialRotationOrchestrator();

        PythonCredentialRotationPublicationResult begun =
            await orchestrator.BeginAsync(
                RotationRequest(fixture, inspected),
                PublicationRequest(fixture, inspected),
                replacement,
                Now);
        PythonCredentialRotationPublicationResult recovered =
            orchestrator.Recover(PublicationRequest(fixture, inspected));

        Assert.Equal("OverlapPublished", begun.Disposition);
        Assert.Equal("RolledBack", recovered.Disposition);
        AssertNoRotationArtifacts();
    }

    [Fact]
    public void ClassifyReturnsActiveOutsideRotationWindow()
    {
        Assert.Equal(
            PythonCredentialLifecycleState.Active,
            Classify(TimeSpan.FromDays(31)));
    }

    [Theory]
    [InlineData(30, PythonCredentialLifecycleState.RotationDue)]
    [InlineData(8, PythonCredentialLifecycleState.RotationDue)]
    [InlineData(7, PythonCredentialLifecycleState.Expiring)]
    [InlineData(1, PythonCredentialLifecycleState.Expiring)]
    public void ClassifyUsesAcceptedRotationAndUrgentWindows(
        int remainingDays,
        PythonCredentialLifecycleState expected)
    {
        Assert.Equal(expected, Classify(TimeSpan.FromDays(remainingDays)));
    }

    [Fact]
    public void ClassifyReturnsExpiredAtExactNotAfterBoundary()
    {
        Assert.Equal(
            PythonCredentialLifecycleState.Expired,
            PythonCredentialLifecycleInspector.Classify(
                Now - TimeSpan.FromDays(90), Now, Now));
    }

    [Fact]
    public void ClassifyReturnsNotYetValidBeforeNotBeforeBoundary()
    {
        Assert.Equal(
            PythonCredentialLifecycleState.NotYetValid,
            PythonCredentialLifecycleInspector.Classify(
                Now + TimeSpan.FromSeconds(1),
                Now + TimeSpan.FromDays(90),
                Now));
    }

    [Fact]
    public void ClassifyAcceptsExactNotBeforeBoundary()
    {
        Assert.Equal(
            PythonCredentialLifecycleState.Active,
            PythonCredentialLifecycleInspector.Classify(
                Now, Now + TimeSpan.FromDays(90), Now));
    }

    [Theory]
    [InlineData("not-before")]
    [InlineData("not-after")]
    [InlineData("now")]
    public void ClassifyRejectsNonUtcTimestamp(string field)
    {
        DateTimeOffset notBefore = Now - TimeSpan.FromDays(1);
        DateTimeOffset notAfter = Now + TimeSpan.FromDays(90);
        DateTimeOffset now = Now;
        DateTimeOffset nonUtc = Now.ToOffset(TimeSpan.FromHours(1));
        if (field == "not-before") notBefore = nonUtc;
        if (field == "not-after") notAfter = nonUtc;
        if (field == "now") now = nonUtc;

        Assert.Throws<ArgumentException>(() =>
            PythonCredentialLifecycleInspector.Classify(
                notBefore, notAfter, now));
    }

    [Fact]
    public void ClassifyRejectsInvertedValidity()
    {
        Assert.Throws<ArgumentException>(() =>
            PythonCredentialLifecycleInspector.Classify(Now, Now, Now));
    }

    [Fact]
    public void InspectionExceptionWithholdsSuppliedCodeFromMessage()
    {
        var exception = new PythonCredentialLifecycleInspectionException(
            "credential-material-invalid");

        Assert.Equal("credential-material-invalid", exception.Code);
        Assert.DoesNotContain(exception.Code, exception.Message,
            StringComparison.Ordinal);
    }

    private static PythonCredentialLifecycleState Classify(TimeSpan remaining) =>
        PythonCredentialLifecycleInspector.Classify(
            Now - TimeSpan.FromDays(60), Now + remaining, Now);

    private static Task<PythonCredentialLifecycleInspectionResult> InspectAsync(
        Fixture fixture) =>
        new PythonCredentialLifecycleInspector().InspectAsync(
            fixture.Request, Now);

    private static PythonCredentialRotationPreparationRequest RotationRequest(
        Fixture fixture,
        PythonCredentialLifecycleInspectionResult inspected) =>
        new(
            fixture.Request,
            fixture.Material.CredentialId,
            inspected.ProfileSha256,
            inspected.EnrollmentSha256,
            inspected.AuthorizationPolicySha256,
            inspected.TrustedServerCertificateSha256);

    private PythonCredentialRotationPublicationRequest PublicationRequest(
        Fixture fixture,
        PythonCredentialLifecycleInspectionResult inspected) =>
        new(
            directory,
            fixture.CertificatePath,
            fixture.PrivateKeyPath,
            fixture.Request.ProfilePath,
            fixture.Request.EnrollmentPath,
            fixture.Request.AuthorizationPolicyPath,
            inspected.ClientCertificateSha256,
            inspected.ClientPrivateKeySha256,
            inspected.ProfileSha256,
            inspected.EnrollmentSha256,
            inspected.AuthorizationPolicySha256);

    private void AssertNoRotationArtifacts() =>
        Assert.DoesNotContain(Directory.EnumerateFiles(directory), path =>
            Path.GetFileName(path).Contains("rotation.transaction",
                StringComparison.Ordinal)
            || path.Contains(".stage-", StringComparison.Ordinal)
            || path.Contains(".backup-", StringComparison.Ordinal)
            || path.Contains(".final-stage-", StringComparison.Ordinal)
            || path.Contains(".overlap-backup-", StringComparison.Ordinal));

    private static string HashFile(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static void AssertEnrollment(
        ReadOnlySpan<byte> document,
        string credentialId,
        bool expected)
    {
        RuntimeHostClientCredentialEnrollmentRegistry registry =
            RuntimeHostClientCredentialEnrollmentRegistryFile.Load(document);
        var identity = new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(credentialId));
        Assert.Equal(expected, registry.TryResolve(
            identity, Now, out RuntimeHostClientPrincipal? principal));
        if (expected)
        {
            Assert.Equal("hase-laptop-python-minipc", principal!.PrincipalId);
            Assert.Equal("private-network-validation-v1", principal.TrustPolicyId);
        }
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private Fixture CreateFixture(
        TimeSpan validity,
        bool includeUnexpectedGrant = false)
    {
        X509Certificate2 root = CreateRoot();
        PythonClientCredentialMaterial material =
            PythonClientCredentialFactory.Create(root, Now, validity);
        string certificate = Path.Combine(directory, "client.pem");
        string privateKey = Path.Combine(directory, "client-key.pem");
        string trustedServer = Path.Combine(directory, "server.cer");
        string profile = Path.Combine(directory, "profile.json");
        string enrollment = Path.Combine(directory, "enrollment.json");
        string policy = Path.Combine(directory, "authorization.json");
        File.WriteAllBytes(certificate, material.CertificatePem.ToArray());
        File.WriteAllBytes(privateKey, material.PrivateKeyPem.ToArray());
        File.WriteAllBytes(trustedServer, root.RawData);
        File.WriteAllText(profile, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            address = "https://192.0.2.20:50443",
            clientCertificate = new
            {
                certificateChainPath = certificate,
                privateKeyPath = privateKey,
            },
            trustedServerCertificate = new { certificatePath = trustedServer },
        }));
        File.WriteAllText(enrollment, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            enrollments = new[]
            {
                new
                {
                    credentialId = material.CredentialId,
                    principalId = "hase-laptop-python-minipc",
                    trustPolicyId = "private-network-validation-v1",
                },
            },
        }));
        string[] expectedGrants =
        [
            "runtime-host.snapshot.read",
            "property.authoritative.read",
            "observation.subscribe",
            "property.write",
            "command.execute",
        ];
        var grants = expectedGrants.Select(permission => new
        {
            principalId = "hase-laptop-python-minipc",
            permission,
        }).ToList();
        if (includeUnexpectedGrant)
        {
            grants.Add(new
            {
                principalId = "hase-laptop-python-minipc",
                permission = "diagnostics.subscribe",
            });
        }
        File.WriteAllText(policy, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            grants,
        }));
        var request = new PythonCredentialLifecycleInspectionRequest(
            profile,
            enrollment,
            policy,
            "hase-laptop-python-minipc",
            "private-network-validation-v1",
            expectedGrants);
        return new Fixture(
            root, material, request, certificate, privateKey, expectedGrants);
    }

    private static X509Certificate2 CreateRoot()
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=HASE Python Lifecycle Test Root",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        return request.CreateSelfSigned(Now.AddYears(-1), Now.AddYears(1));
    }

    private sealed record Fixture(
        X509Certificate2 Root,
        PythonClientCredentialMaterial Material,
        PythonCredentialLifecycleInspectionRequest Request,
        string CertificatePath,
        string PrivateKeyPath,
        IReadOnlyList<string> ExpectedGrants) : IDisposable
    {
        public void Dispose()
        {
            Material.Dispose();
            Root.Dispose();
        }
    }
}
