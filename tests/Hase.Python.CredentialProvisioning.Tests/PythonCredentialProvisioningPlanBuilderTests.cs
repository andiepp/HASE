using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Hase.Python.CredentialProvisioning;

namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonCredentialProvisioningPlanBuilderTests : IDisposable
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);

    private const string CandidateCredentialId =
        "x509-sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "hase-python-plan-tests",
        Guid.NewGuid().ToString("N"));

    public PythonCredentialProvisioningPlanBuilderTests()
    {
        Directory.CreateDirectory(directory);
    }

    [Fact]
    public async Task CreateAsync_ValidInputs_ReturnsDeterministicLeastPrivilegePlan()
    {
        using X509Certificate2 root = CreateRsaRoot();
        using X509Certificate2 trusted = X509CertificateLoader.LoadCertificate(root.RawData);
        Fixture fixture = CreateFixture(root.Thumbprint);
        var builder = new PythonCredentialProvisioningPlanBuilder();

        PythonCredentialProvisioningPlan first = await builder.CreateAsync(
            fixture.Request,
            Now,
            [root],
            [trusted]);
        PythonCredentialProvisioningPlan second = await builder.CreateAsync(
            fixture.Request,
            Now,
            [root],
            [trusted]);

        Assert.Equal(first, second);
        Assert.Equal(
            "python-provisioning-plan-sha256:",
            first.PlanId[..(first.PlanId.IndexOf(':') + 1)]);
        Assert.Equal(96, first.PlanId.Length);
        Assert.Equal(2048, first.LeafRsaKeySize);
        Assert.Equal("sha256WithRSAEncryption", first.LeafSignatureAlgorithm);
        Assert.Equal(["1.3.6.1.5.5.7.3.2"], first.LeafEnhancedKeyUsages);
        Assert.Equal(
            ["runtime-host.snapshot.read", "property.authoritative.read"],
            first.AuthorizationGrants);
        Assert.Equal(Now.AddMinutes(-5), first.NotBeforeUtc);
        Assert.Equal(Now.AddMinutes(-5).AddDays(30), first.NotAfterUtc);
        Assert.Equal(
            HashFile(fixture.Request.SourceProfilePath),
            first.SourceProfileSha256);
        Assert.Equal(
            HashFile(fixture.Request.EnrollmentPath),
            first.EnrollmentSha256);
        Assert.Equal(fixture.PolicyHash, first.AuthorizationPolicySha256);
        Assert.False(first.AllowReplacement);
        AssertNoMutation(fixture);
    }

    [Fact]
    public async Task CreateAsync_ReplacementAuthorization_ChangesPlanIdentity()
    {
        using X509Certificate2 root = CreateRsaRoot();
        using X509Certificate2 trusted =
            X509CertificateLoader.LoadCertificate(root.RawData);
        Fixture fixture = CreateFixture(root.Thumbprint);
        var builder = new PythonCredentialProvisioningPlanBuilder();

        PythonCredentialProvisioningPlan withoutReplacement =
            await builder.CreateAsync(
                fixture.Request,
                Now,
                [root],
                [trusted]);
        PythonCredentialProvisioningPlan withReplacement =
            await builder.CreateAsync(
                fixture.Request with { AllowReplacement = true },
                Now,
                [root],
                [trusted]);

        Assert.NotEqual(withoutReplacement.PlanId, withReplacement.PlanId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(91)]
    public async Task CreateAsync_InvalidValidity_Rejects(int days)
    {
        using X509Certificate2 root = CreateRsaRoot();
        using X509Certificate2 trusted = X509CertificateLoader.LoadCertificate(root.RawData);
        Fixture fixture = CreateFixture(root.Thumbprint);

        await AssertFailure(
            fixture.Request with { Validity = TimeSpan.FromDays(days) },
            Now,
            [root],
            [trusted],
            "validity-invalid");
    }

    [Fact]
    public async Task CreateAsync_AmbiguousSigningRoot_Rejects()
    {
        using X509Certificate2 root = CreateRsaRoot();
        const string password = "duplicate-test-root";
        using X509Certificate2 duplicate = X509CertificateLoader.LoadPkcs12(
            root.Export(X509ContentType.Pkcs12, password),
            password);
        using X509Certificate2 trusted = X509CertificateLoader.LoadCertificate(root.RawData);
        Fixture fixture = CreateFixture(root.Thumbprint);

        await AssertFailure(
            fixture.Request,
            Now,
            [root, duplicate],
            [trusted],
            "signing-root-not-unique");
    }

    [Theory]
    [InlineData("keyless")]
    [InlineData("non-ca")]
    [InlineData("expired")]
    [InlineData("ecdsa")]
    [InlineData("no-key-cert-sign")]
    public async Task CreateAsync_UnusableSigningRoot_Rejects(string kind)
    {
        using X509Certificate2 original = kind switch
        {
            "non-ca" => CreateRsaRoot(certificateAuthority: false),
            "expired" => CreateRsaRoot(notBefore: Now.AddYears(-2), notAfter: Now.AddYears(-1)),
            "ecdsa" => CreateEcdsaRoot(),
            "no-key-cert-sign" => CreateRsaRoot(keyUsage: X509KeyUsageFlags.CrlSign),
            _ => CreateRsaRoot(),
        };
        using X509Certificate2 root = kind == "keyless"
            ? X509CertificateLoader.LoadCertificate(original.RawData)
            : X509CertificateLoader.LoadPkcs12(
                original.Export(X509ContentType.Pkcs12),
                password: null,
                X509KeyStorageFlags.EphemeralKeySet);
        using X509Certificate2 trusted = X509CertificateLoader.LoadCertificate(root.RawData);
        Fixture fixture = CreateFixture(root.Thumbprint);

        await AssertFailure(
            fixture.Request,
            Now,
            [root],
            [trusted],
            "signing-root-unusable");
    }

    [Fact]
    public async Task CreateAsync_UntrustedSigningRoot_Rejects()
    {
        using X509Certificate2 root = CreateRsaRoot();
        Fixture fixture = CreateFixture(root.Thumbprint);

        await AssertFailure(
            fixture.Request,
            Now,
            [root],
            [],
            "signing-root-not-trusted");
    }

    [Fact]
    public async Task CreateAsync_PolicyRevisionMismatch_Rejects()
    {
        using X509Certificate2 root = CreateRsaRoot();
        using X509Certificate2 trusted = X509CertificateLoader.LoadCertificate(root.RawData);
        Fixture fixture = CreateFixture(root.Thumbprint);
        File.AppendAllText(fixture.Request.AuthorizationPolicyPath, " ");

        await AssertFailure(
            fixture.Request,
            Now,
            [root],
            [trusted],
            "authorization-policy-revision-mismatch");
    }

    [Fact]
    public async Task CreateAsync_AlreadyEnrolledCredential_Rejects()
    {
        using X509Certificate2 root = CreateRsaRoot();
        using X509Certificate2 trusted = X509CertificateLoader.LoadCertificate(root.RawData);
        Fixture fixture = CreateFixture(
            root.Thumbprint,
            enrollmentCredentialId: CandidateCredentialId);

        await AssertFailure(
            fixture.Request,
            Now,
            [root],
            [trusted],
            "credential-already-enrolled");
    }

    [Theory]
    [InlineData("runtime-host.snapshot.read")]
    [InlineData("property.cached.read")]
    [InlineData("property.authoritative.read")]
    [InlineData("property.write")]
    [InlineData("command.execute")]
    [InlineData("observation.subscribe")]
    [InlineData("diagnostics.subscribe")]
    public async Task CreateAsync_PrincipalWithAnyExistingGrant_Rejects(
        string permission)
    {
        using X509Certificate2 root = CreateRsaRoot();
        using X509Certificate2 trusted = X509CertificateLoader.LoadCertificate(root.RawData);
        Fixture fixture = CreateFixture(root.Thumbprint, principalPermission: permission);

        await AssertFailure(
            fixture.Request,
            Now,
            [root],
            [trusted],
            "principal-already-authorized");
    }

    [Theory]
    [InlineData("thumbprint")]
    [InlineData("credential")]
    [InlineData("principal")]
    [InlineData("trust-policy")]
    [InlineData("policy-hash")]
    public async Task CreateAsync_InvalidIdentityInput_UsesSanitizedFailure(
        string field)
    {
        using X509Certificate2 root = CreateRsaRoot();
        using X509Certificate2 trusted = X509CertificateLoader.LoadCertificate(root.RawData);
        Fixture fixture = CreateFixture(root.Thumbprint);
        PythonCredentialProvisioningPlanRequest request = field switch
        {
            "thumbprint" => fixture.Request with { SigningRootThumbprint = "invalid" },
            "credential" => fixture.Request with { CredentialId = "invalid" },
            "principal" => fixture.Request with { PrincipalId = " " },
            "trust-policy" => fixture.Request with { TrustPolicyId = " " },
            _ => fixture.Request with { ExpectedAuthorizationPolicySha256 = "invalid" },
        };

        PythonCredentialProvisioningPlanException exception = await Assert.ThrowsAsync<PythonCredentialProvisioningPlanException>(
            () => new PythonCredentialProvisioningPlanBuilder().CreateAsync(
                request,
                Now,
                [root],
                [trusted]));

        Assert.DoesNotContain(directory, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task CreateAsync_NonUtcTimestamp_Rejects()
    {
        using X509Certificate2 root = CreateRsaRoot();
        using X509Certificate2 trusted = X509CertificateLoader.LoadCertificate(root.RawData);
        Fixture fixture = CreateFixture(root.Thumbprint);

        await AssertFailure(
            fixture.Request,
            Now.ToOffset(TimeSpan.FromHours(2)),
            [root],
            [trusted],
            "timestamp-not-utc");
    }

    [Fact]
    public async Task CreateAsync_OverlappingSecurityAndOutputPaths_Rejects()
    {
        using X509Certificate2 root = CreateRsaRoot();
        using X509Certificate2 trusted = X509CertificateLoader.LoadCertificate(root.RawData);
        Fixture fixture = CreateFixture(root.Thumbprint);

        await AssertFailure(
            fixture.Request with
            {
                ProfilePath = fixture.Request.AuthorizationPolicyPath,
                AllowReplacement = true,
            },
            Now,
            [root],
            [trusted],
            "paths-not-distinct");
    }

    [Fact]
    public async Task CreateAsync_InvalidSourceProfile_RejectsWithoutMutation()
    {
        using X509Certificate2 root = CreateRsaRoot();
        using X509Certificate2 trusted = X509CertificateLoader.LoadCertificate(root.RawData);
        Fixture fixture = CreateFixture(root.Thumbprint);
        File.WriteAllText(fixture.Request.SourceProfilePath, "{ invalid");

        await AssertFailure(
            fixture.Request,
            Now,
            [root],
            [trusted],
            "source-profile-invalid");
        AssertNoMutation(fixture);
    }

    [Theory]
    [InlineData("source-profile")]
    [InlineData("enrollment")]
    public async Task CreateAsync_ChangedInputBytes_ChangesPlanIdentity(
        string input)
    {
        using X509Certificate2 root = CreateRsaRoot();
        using X509Certificate2 trusted = X509CertificateLoader.LoadCertificate(root.RawData);
        Fixture fixture = CreateFixture(root.Thumbprint);
        var builder = new PythonCredentialProvisioningPlanBuilder();
        PythonCredentialProvisioningPlan original = await builder.CreateAsync(
            fixture.Request,
            Now,
            [root],
            [trusted]);

        string changedPath = input == "source-profile"
            ? fixture.Request.SourceProfilePath
            : fixture.Request.EnrollmentPath;
        File.AppendAllText(changedPath, " ");

        PythonCredentialProvisioningPlan changed = await builder.CreateAsync(
            fixture.Request,
            Now,
            [root],
            [trusted]);

        Assert.NotEqual(original.PlanId, changed.PlanId);
        Assert.NotEqual(
            input == "source-profile"
                ? original.SourceProfileSha256
                : original.EnrollmentSha256,
            input == "source-profile"
                ? changed.SourceProfileSha256
                : changed.EnrollmentSha256);
        AssertNoMutation(fixture);
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }

    private Fixture CreateFixture(
        string signingRootThumbprint,
        string enrollmentCredentialId =
            "x509-sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
        string? principalPermission = null)
    {
        string sourceProfile = Path.Combine(directory, "source-profile.json");
        string trustedServer = Path.Combine(directory, "trusted-server.pem");
        string oldCertificate = Path.Combine(directory, "old-client.pem");
        string oldPrivateKey = Path.Combine(directory, "old-key.pem");
        string enrollment = Path.Combine(directory, "enrollment.json");
        string policy = Path.Combine(directory, "authorization.json");
        string certificate = Path.Combine(directory, "python-client.pem");
        string privateKey = Path.Combine(directory, "python-key.pem");
        string profile = Path.Combine(directory, "python-profile.json");

        File.WriteAllText(trustedServer, "trusted server");
        File.WriteAllText(oldCertificate, "old certificate");
        File.WriteAllText(oldPrivateKey, "old key");
        File.WriteAllText(sourceProfile, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            address = "https://192.0.2.10:50443",
            clientCertificate = new
            {
                certificateChainPath = oldCertificate,
                privateKeyPath = oldPrivateKey,
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
                    credentialId = enrollmentCredentialId,
                    principalId = "existing-client",
                    trustPolicyId = "private-network-validation-v1",
                },
            },
        }));
        File.WriteAllText(policy, JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            grants = principalPermission is null
                ? new[]
                {
                    new
                    {
                        principalId = "existing-client",
                        permission = "runtime-host.snapshot.read",
                    },
                }
                : new[]
                {
                    new
                    {
                        principalId = "hase-python-automation",
                        permission = principalPermission,
                    },
                },
        }));
        string policyHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(policy)));

        return new Fixture(
            new PythonCredentialProvisioningPlanRequest(
                signingRootThumbprint,
                CandidateCredentialId,
                "hase-python-automation",
                "private-network-validation-v1",
                sourceProfile,
                directory,
                certificate,
                privateKey,
                profile,
                enrollment,
                policy,
                policyHash,
                TimeSpan.FromDays(30)),
            policyHash,
            certificate,
            privateKey,
            profile);
    }

    private static X509Certificate2 CreateRsaRoot(
        bool certificateAuthority = true,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        X509KeyUsageFlags keyUsage = X509KeyUsageFlags.KeyCertSign)
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=HASE Python Plan Test Root",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, true));
        return request.CreateSelfSigned(
            notBefore ?? Now.AddYears(-1),
            notAfter ?? Now.AddYears(1));
    }

    private static X509Certificate2 CreateEcdsaRoot()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(
            "CN=HASE Python Plan ECDSA Test Root",
            key,
            HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return request.CreateSelfSigned(Now.AddYears(-1), Now.AddYears(1));
    }

    private static async Task AssertFailure(
        PythonCredentialProvisioningPlanRequest request,
        DateTimeOffset utcNow,
        IEnumerable<X509Certificate2> personal,
        IEnumerable<X509Certificate2> trusted,
        string code)
    {
        PythonCredentialProvisioningPlanException exception = await Assert.ThrowsAsync<PythonCredentialProvisioningPlanException>(
            () => new PythonCredentialProvisioningPlanBuilder().CreateAsync(
                request,
                utcNow,
                personal,
                trusted));
        Assert.Equal(code, exception.Code);
    }

    private static string HashFile(string path) =>
        Convert.ToHexStringLower(
            SHA256.HashData(File.ReadAllBytes(path)));

    private void AssertNoMutation(Fixture fixture)
    {
        Assert.False(File.Exists(fixture.CertificatePath));
        Assert.False(File.Exists(fixture.PrivateKeyPath));
        Assert.False(File.Exists(fixture.ProfilePath));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(directory),
            path => path.Contains(".stage-", StringComparison.Ordinal)
                || path.Contains(".backup-", StringComparison.Ordinal));
    }

    private sealed record Fixture(
        PythonCredentialProvisioningPlanRequest Request,
        string PolicyHash,
        string CertificatePath,
        string PrivateKeyPath,
        string ProfilePath);
}
