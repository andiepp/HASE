using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Hase.Runtime.Remote.Grpc.Adapter;

namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonCredentialProvisioningPreparerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(
        2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(
        Path.GetTempPath(), $"hase-python-preparer-{Guid.NewGuid():N}");

    public PythonCredentialProvisioningPreparerTests() =>
        Directory.CreateDirectory(directory);

    [Fact]
    public async Task PrepareAsync_ApprovedPlan_ReturnsFiveValidatedCandidatesWithoutMutation()
    {
        using Fixture fixture = await CreateFixtureAsync();

        using PythonCredentialProvisioningCandidates candidates =
            await PrepareAsync(fixture);

        Assert.Equal(fixture.Material.CertificatePem.ToArray(),
            candidates.CertificatePem.ToArray());
        Assert.Equal(fixture.Material.PrivateKeyPem.ToArray(),
            candidates.PrivateKeyPem.ToArray());
        PythonRuntimeHostProfileDocument profile =
            PythonRuntimeHostProfileDocument.Load(candidates.ProfileDocument.Span);
        Assert.Equal(fixture.Plan.CertificatePath,
            profile.ClientCertificateChainPath);
        Assert.Equal(fixture.Plan.PrivateKeyPath, profile.ClientPrivateKeyPath);

        var identity = new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(fixture.Plan.CredentialId));
        RuntimeHostClientCredentialEnrollmentRegistry registry =
            RuntimeHostClientCredentialEnrollmentRegistryFile.Load(
                candidates.EnrollmentDocument.Span);
        Assert.True(registry.TryResolve(identity, Now, out RuntimeHostClientPrincipal? principal));
        Assert.Equal("hase-python-automation", principal!.PrincipalId);
        var existingIdentity = new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(
                "x509-sha256:" + new string('b', 64)));
        Assert.True(registry.TryResolve(
            existingIdentity,
            Now,
            out RuntimeHostClientPrincipal? existingPrincipal));
        Assert.Equal("existing-client", existingPrincipal!.PrincipalId);

        RuntimeHostAuthorizationPolicy policy = RuntimeHostAuthorizationPolicyFile.Load(
            candidates.AuthorizationPolicyDocument.Span);
        Assert.True(policy.IsGranted("existing-client",
            RuntimeHostPermission.ReadSnapshot));
        Assert.True(policy.IsGranted("hase-python-automation",
            RuntimeHostPermission.ReadSnapshot));
        Assert.True(policy.IsGranted("hase-python-automation",
            RuntimeHostPermission.ReadAuthoritativeProperty));
        Assert.False(policy.IsGranted("hase-python-automation",
            RuntimeHostPermission.WriteProperty));
        AssertNoPublishedOutputs(fixture.Plan);
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("enrollment")]
    [InlineData("policy")]
    public async Task PrepareAsync_ChangedLockedInput_Rejects(string input)
    {
        using Fixture fixture = await CreateFixtureAsync();
        File.AppendAllText(input switch
        {
            "profile" => fixture.Plan.SourceProfilePath,
            "enrollment" => fixture.Plan.EnrollmentPath,
            _ => fixture.Plan.AuthorizationPolicyPath,
        }, " ");

        PythonCredentialProvisioningPreparationException exception =
            await Assert.ThrowsAsync<PythonCredentialProvisioningPreparationException>(
                () => PrepareAsync(fixture));

        Assert.Equal("input-revision-mismatch", exception.Code);
        AssertNoPublishedOutputs(fixture.Plan);
    }

    [Fact]
    public async Task PrepareAsync_MismatchedMaterial_Rejects()
    {
        using Fixture fixture = await CreateFixtureAsync();
        using PythonClientCredentialMaterial other = PythonClientCredentialFactory.Create(
            fixture.Root, Now, TimeSpan.FromDays(30));

        PythonCredentialProvisioningPreparationException exception =
            await Assert.ThrowsAsync<PythonCredentialProvisioningPreparationException>(() =>
                new PythonCredentialProvisioningPreparer().PrepareAsync(
                    fixture.Plan, other, [fixture.Root], [fixture.Root]));

        Assert.Equal("credential-id-mismatch", exception.Code);
    }

    [Fact]
    public async Task PrepareAsync_AlteredApprovedPlan_Rejects()
    {
        using Fixture fixture = await CreateFixtureAsync();
        PythonCredentialProvisioningPlan altered = fixture.Plan with
        {
            ProfilePath = Path.Combine(directory, "altered-profile.json"),
        };

        PythonCredentialProvisioningPreparationException exception =
            await Assert.ThrowsAsync<PythonCredentialProvisioningPreparationException>(() =>
                new PythonCredentialProvisioningPreparer().PrepareAsync(
                    altered, fixture.Material, [fixture.Root], [fixture.Root]));

        Assert.Equal("plan-revision-invalid", exception.Code);
    }

    [Fact]
    public async Task PrepareAsync_NonPairingPrivateKey_Rejects()
    {
        using Fixture fixture = await CreateFixtureAsync();
        using PythonClientCredentialMaterial other = PythonClientCredentialFactory.Create(
            fixture.Root, Now, TimeSpan.FromDays(30));
        using var mismatched = new PythonClientCredentialMaterial(
            fixture.Material.CertificatePem.ToArray(),
            other.PrivateKeyPem.ToArray(),
            fixture.Material.CredentialId);

        PythonCredentialProvisioningPreparationException exception =
            await Assert.ThrowsAsync<PythonCredentialProvisioningPreparationException>(() =>
                new PythonCredentialProvisioningPreparer().PrepareAsync(
                    fixture.Plan, mismatched, [fixture.Root], [fixture.Root]));

        Assert.Equal("credential-material-invalid", exception.Code);
    }

    [Fact]
    public async Task PrepareAsync_ExistingPythonAuthority_Rejects()
    {
        using Fixture fixture = await CreateFixtureAsync();
        File.WriteAllText(fixture.Plan.AuthorizationPolicyPath, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            grants = new[]
            {
                new { principalId = "existing-client", permission = "runtime-host.snapshot.read" },
                new { principalId = "hase-python-automation", permission = "property.cached.read" },
            },
        }));
        PythonCredentialProvisioningPlan changedPlan = fixture.Plan with
        {
            AuthorizationPolicySha256 = HashFile(fixture.Plan.AuthorizationPolicyPath),
        };
        changedPlan = changedPlan with
        {
            PlanId = PythonCredentialProvisioningPreparer.CalculatePlanId(
                changedPlan,
                fixture.Root),
        };

        PythonCredentialProvisioningPreparationException exception =
            await Assert.ThrowsAsync<PythonCredentialProvisioningPreparationException>(() =>
                new PythonCredentialProvisioningPreparer().PrepareAsync(
                    changedPlan, fixture.Material, [fixture.Root], [fixture.Root]));

        Assert.Equal("python-authority-already-present", exception.Code);
    }

    [Fact]
    public async Task Dispose_ZerosAllCandidateBuffersAndRejectsAccess()
    {
        using Fixture fixture = await CreateFixtureAsync();
        PythonCredentialProvisioningCandidates candidates = await PrepareAsync(fixture);
        ReadOnlyMemory<byte>[] buffers =
        [
            candidates.CertificatePem,
            candidates.PrivateKeyPem,
            candidates.ProfileDocument,
            candidates.EnrollmentDocument,
            candidates.AuthorizationPolicyDocument,
        ];

        candidates.Dispose();
        candidates.Dispose();

        foreach (ReadOnlyMemory<byte> buffer in buffers)
        {
            Assert.All(buffer.ToArray(), value => Assert.Equal(0, value));
        }
        Assert.Throws<ObjectDisposedException>(() => candidates.CertificatePem);
        Assert.Throws<ObjectDisposedException>(() => candidates.PrivateKeyPem);
        Assert.Throws<ObjectDisposedException>(() => candidates.ProfileDocument);
        Assert.Throws<ObjectDisposedException>(() => candidates.EnrollmentDocument);
        Assert.Throws<ObjectDisposedException>(() => candidates.AuthorizationPolicyDocument);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private async Task<Fixture> CreateFixtureAsync()
    {
        X509Certificate2 root = CreateRoot();
        PythonClientCredentialMaterial material = PythonClientCredentialFactory.Create(
            root, Now, TimeSpan.FromDays(30));
        string trusted = Write("trusted.pem", "trusted");
        string oldCertificate = Write("old-client.pem", "old certificate");
        string oldKey = Write("old-key.pem", "old key");
        string profile = Path.Combine(directory, "source-profile.json");
        File.WriteAllText(profile, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            address = "https://192.0.2.10:50443",
            clientCertificate = new
            {
                certificateChainPath = oldCertificate,
                privateKeyPath = oldKey,
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
        var request = new PythonCredentialProvisioningPlanRequest(
            root.Thumbprint,
            material.CredentialId,
            "hase-python-automation",
            "private-network-validation-v1",
            profile,
            directory,
            Path.Combine(directory, "python-client.pem"),
            Path.Combine(directory, "python-key.pem"),
            Path.Combine(directory, "python-profile.json"),
            enrollment,
            policy,
            HashFile(policy),
            TimeSpan.FromDays(30));
        PythonCredentialProvisioningPlan plan =
            await new PythonCredentialProvisioningPlanBuilder().CreateAsync(
                request, Now, [root], [root]);
        return new Fixture(root, material, plan);
    }

    private Task<PythonCredentialProvisioningCandidates> PrepareAsync(Fixture fixture) =>
        new PythonCredentialProvisioningPreparer().PrepareAsync(
            fixture.Plan, fixture.Material, [fixture.Root], [fixture.Root]);

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
            "CN=HASE Python Preparation Test Root",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        return request.CreateSelfSigned(Now.AddYears(-1), Now.AddYears(1));
    }

    private static void AssertNoPublishedOutputs(PythonCredentialProvisioningPlan plan)
    {
        Assert.False(File.Exists(plan.CertificatePath));
        Assert.False(File.Exists(plan.PrivateKeyPath));
        Assert.False(File.Exists(plan.ProfilePath));
        Assert.DoesNotContain(Directory.EnumerateFiles(plan.ProvisioningDirectory),
            path => path.Contains(".stage-", StringComparison.Ordinal)
                || path.Contains(".backup-", StringComparison.Ordinal));
    }

    private sealed record Fixture(
        X509Certificate2 Root,
        PythonClientCredentialMaterial Material,
        PythonCredentialProvisioningPlan Plan) : IDisposable
    {
        public void Dispose()
        {
            Material.Dispose();
            Root.Dispose();
        }
    }
}
