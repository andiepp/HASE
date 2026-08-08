using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.Json;
using Hase.Python.CredentialProvisioning;

namespace Hase.Python.CredentialProvisioning.Tests;

public sealed class PythonCredentialProvisionerTests : IDisposable
{
    private readonly string testDirectory =
        Path.Combine(
            Path.GetTempPath(),
            "hase-python-provisioning-tests",
            Guid.NewGuid().ToString("N"));

    public PythonCredentialProvisionerTests()
    {
        Directory.CreateDirectory(testDirectory);
    }

    [Fact]
    public void Provision_NewTargets_PublishesCredentialAndStrictVersionOneProfile()
    {
        Fixture fixture = CreateFixture();
        using PythonClientCredentialMaterial material = CreateMaterial();

        PythonCredentialProvisioningResult result =
            new PythonCredentialProvisioner().Provision(
                fixture.Request,
                material);

        Assert.Equal(material.CertificatePem.ToArray(), File.ReadAllBytes(fixture.Certificate));
        Assert.Equal(material.PrivateKeyPem.ToArray(), File.ReadAllBytes(fixture.PrivateKey));
        Assert.Equal(material.CredentialId, result.CredentialId);
        Assert.False(result.ReplacedExistingFiles);

        using JsonDocument profile = JsonDocument.Parse(File.ReadAllBytes(fixture.Profile));
        JsonElement root = profile.RootElement;
        Assert.Equal(
            ["address", "clientCertificate", "formatVersion", "trustedServerCertificate"],
            root.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.False(root.TryGetProperty("credentialId", out _));
        Assert.Equal(1, root.GetProperty("formatVersion").GetInt32());
        Assert.Equal("https://192.0.2.10:50443", root.GetProperty("address").GetString());
        Assert.Equal(
            fixture.Certificate,
            root.GetProperty("clientCertificate").GetProperty("certificateChainPath").GetString());
        Assert.Equal(
            fixture.PrivateKey,
            root.GetProperty("clientCertificate").GetProperty("privateKeyPath").GetString());
        Assert.Equal(
            fixture.TrustedServer,
            root.GetProperty("trustedServerCertificate").GetProperty("certificatePath").GetString());
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void Provision_ExistingTargetsWithAuthorization_ReplacesAllOutputs()
    {
        Fixture fixture = CreateFixture(allowReplacement: true);
        WriteOriginalTargets(fixture);
        using PythonClientCredentialMaterial material = CreateMaterial();

        PythonCredentialProvisioningResult result =
            new PythonCredentialProvisioner().Provision(fixture.Request, material);

        Assert.True(result.ReplacedExistingFiles);
        Assert.Equal(material.CertificatePem.ToArray(), File.ReadAllBytes(fixture.Certificate));
        Assert.Equal(material.PrivateKeyPem.ToArray(), File.ReadAllBytes(fixture.PrivateKey));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void Provision_ExistingTargetWithoutAuthorization_RejectsWithoutChanges()
    {
        Fixture fixture = CreateFixture();
        File.WriteAllText(fixture.Certificate, "original-certificate");
        using PythonClientCredentialMaterial material = CreateMaterial();

        PythonCredentialProvisioningException exception = Assert.Throws<PythonCredentialProvisioningException>(
            () => new PythonCredentialProvisioner().Provision(fixture.Request, material));

        Assert.Equal("replacement-not-authorized", exception.Code);
        Assert.Equal("original-certificate", File.ReadAllText(fixture.Certificate));
        Assert.False(File.Exists(fixture.PrivateKey));
        Assert.False(File.Exists(fixture.Profile));
        AssertNoTransactionArtifacts();
    }

    [Theory]
    [InlineData(4)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Provision_InjectedFailure_RestoresEveryOriginal(
        int failureStepValue)
    {
        var failureStep =
            (PythonCredentialProvisioningStep)failureStepValue;
        Fixture fixture = CreateFixture(allowReplacement: true);
        WriteOriginalTargets(fixture);
        using PythonClientCredentialMaterial material = CreateMaterial();
        var provisioner = new PythonCredentialProvisioner(
            step =>
            {
                if (step == failureStep)
                {
                    throw new IOException("injected test failure");
                }
            });

        PythonCredentialProvisioningException exception = Assert.Throws<PythonCredentialProvisioningException>(
            () => provisioner.Provision(fixture.Request, material));

        Assert.Equal("transaction-failed", exception.Code);
        Assert.Equal("original-certificate", File.ReadAllText(fixture.Certificate));
        Assert.Equal("original-private-key", File.ReadAllText(fixture.PrivateKey));
        Assert.Equal("original-profile", File.ReadAllText(fixture.Profile));
        Assert.DoesNotContain(testDirectory, exception.Message, StringComparison.OrdinalIgnoreCase);
        AssertNoTransactionArtifacts();
    }

    [Theory]
    [InlineData("certificate")]
    [InlineData("private-key")]
    [InlineData("profile")]
    public void Provision_RelativeTarget_RejectsBeforeOutput(string target)
    {
        Fixture fixture = CreateFixture();
        PythonCredentialProvisioningRequest request = target switch
        {
            "certificate" => fixture.Request with { CertificatePath = "client.pem" },
            "private-key" => fixture.Request with { PrivateKeyPath = "client-key.pem" },
            _ => fixture.Request with { ProfilePath = "profile.json" },
        };
        using PythonClientCredentialMaterial material = CreateMaterial();

        AssertFailure(request, material, "path-invalid");
        AssertNoOutputs(fixture);
    }

    [Fact]
    public void Provision_TargetOutsideExplicitDirectory_RejectsBeforeOutput()
    {
        Fixture fixture = CreateFixture();
        string outside = Path.Combine(Path.GetDirectoryName(testDirectory)!, "outside.pem");
        using PythonClientCredentialMaterial material = CreateMaterial();

        AssertFailure(
            fixture.Request with { CertificatePath = outside },
            material,
            "target-outside-provisioning-directory");
        AssertNoOutputs(fixture);
    }

    [Fact]
    public void Provision_OverlappingTargets_RejectsBeforeOutput()
    {
        Fixture fixture = CreateFixture();
        using PythonClientCredentialMaterial material = CreateMaterial();

        AssertFailure(
            fixture.Request with { PrivateKeyPath = fixture.Certificate },
            material,
            "target-paths-not-distinct");
        AssertNoOutputs(fixture);
    }

    [Fact]
    public void Provision_DirectoryTarget_RejectsBeforeOutput()
    {
        Fixture fixture = CreateFixture();
        string directoryTarget = Path.Combine(testDirectory, "directory-target");
        Directory.CreateDirectory(directoryTarget);
        using PythonClientCredentialMaterial material = CreateMaterial();

        AssertFailure(
            fixture.Request with { CertificatePath = directoryTarget },
            material,
            "target-path-invalid");
        AssertNoOutputs(fixture);
    }

    [Fact]
    public void Provision_ReparsePointParent_RejectsBeforeOutput()
    {
        Fixture fixture = CreateFixture();
        string realDirectory = Path.Combine(testDirectory, "real");
        string linkedDirectory = Path.Combine(testDirectory, "linked");
        Directory.CreateDirectory(realDirectory);

        try
        {
            Directory.CreateSymbolicLink(linkedDirectory, realDirectory);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
            or IOException
            or PlatformNotSupportedException)
        {
            return;
        }

        using PythonClientCredentialMaterial material = CreateMaterial();
        AssertFailure(
            fixture.Request with
            {
                CertificatePath = Path.Combine(linkedDirectory, "client.pem"),
            },
            material,
            "target-path-invalid");
        AssertNoOutputs(fixture);
    }

    [Fact]
    public void Provision_InvalidSourceProfile_UsesSanitizedFailure()
    {
        Fixture fixture = CreateFixture();
        File.WriteAllText(fixture.SourceProfile, "{ invalid");
        using PythonClientCredentialMaterial material = CreateMaterial();

        PythonCredentialProvisioningException exception = Assert.Throws<PythonCredentialProvisioningException>(
            () => new PythonCredentialProvisioner().Provision(fixture.Request, material));

        Assert.Equal("source-profile-invalid", exception.Code);
        Assert.DoesNotContain(testDirectory, exception.Message, StringComparison.OrdinalIgnoreCase);
        AssertNoOutputs(fixture);
    }

    [Fact]
    public void Provision_PrivateKey_IsRestrictedToCurrentUser()
    {
        Fixture fixture = CreateFixture();
        using PythonClientCredentialMaterial material = CreateMaterial();

        new PythonCredentialProvisioner().Provision(fixture.Request, material);

        if (OperatingSystem.IsWindows())
        {
            FileSecurity security = new FileInfo(fixture.PrivateKey).GetAccessControl();
            Assert.True(security.AreAccessRulesProtected);
            SecurityIdentifier owner = Assert.IsType<SecurityIdentifier>(security.GetOwner(typeof(SecurityIdentifier)));
            Assert.Equal(WindowsIdentity.GetCurrent().User, owner);
            AuthorizationRuleCollection rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier));
            FileSystemAccessRule rule = Assert.Single(rules.Cast<FileSystemAccessRule>());
            Assert.Equal(owner, rule.IdentityReference);
            Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        }
        else
        {
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(fixture.PrivateKey));
        }
    }

    public void Dispose()
    {
        Directory.Delete(testDirectory, recursive: true);
    }

    private Fixture CreateFixture(bool allowReplacement = false)
    {
        string trustedServer = Path.Combine(testDirectory, "trusted-server.pem");
        string sourceProfile = Path.Combine(testDirectory, "source-profile.json");
        string certificate = Path.Combine(testDirectory, "client-chain.pem");
        string privateKey = Path.Combine(testDirectory, "client-key.pem");
        string profile = Path.Combine(testDirectory, "runtime-host-profile.json");
        string oldCertificate = Path.Combine(testDirectory, "old-chain.pem");
        string oldPrivateKey = Path.Combine(testDirectory, "old-key.pem");
        File.WriteAllText(trustedServer, "trusted-server-certificate");
        File.WriteAllText(oldCertificate, "old-client-certificate");
        File.WriteAllText(oldPrivateKey, "old-client-private-key");
        File.WriteAllText(
            sourceProfile,
            JsonSerializer.Serialize(
                new
                {
                    formatVersion = 1,
                    address = "https://192.0.2.10:50443",
                    clientCertificate = new
                    {
                        certificateChainPath = oldCertificate,
                        privateKeyPath = oldPrivateKey,
                    },
                    trustedServerCertificate = new
                    {
                        certificatePath = trustedServer,
                    },
                }));

        return new Fixture(
            new PythonCredentialProvisioningRequest(
                testDirectory,
                sourceProfile,
                certificate,
                privateKey,
                profile,
                allowReplacement),
            sourceProfile,
            trustedServer,
            certificate,
            privateKey,
            profile);
    }

    private static PythonClientCredentialMaterial CreateMaterial()
    {
        using RSA rootKey = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test HASE Python Root",
            rootKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using X509Certificate2 root = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));
        return PythonClientCredentialFactory.Create(
            root,
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(30));
    }

    private static void WriteOriginalTargets(Fixture fixture)
    {
        File.WriteAllText(fixture.Certificate, "original-certificate");
        File.WriteAllText(fixture.PrivateKey, "original-private-key");
        File.WriteAllText(fixture.Profile, "original-profile");
    }

    private static void AssertFailure(
        PythonCredentialProvisioningRequest request,
        PythonClientCredentialMaterial material,
        string code)
    {
        PythonCredentialProvisioningException exception = Assert.Throws<PythonCredentialProvisioningException>(
            () => new PythonCredentialProvisioner().Provision(request, material));
        Assert.Equal(code, exception.Code);
    }

    private static void AssertNoOutputs(Fixture fixture)
    {
        Assert.False(File.Exists(fixture.Certificate));
        Assert.False(File.Exists(fixture.PrivateKey));
        Assert.False(File.Exists(fixture.Profile));
    }

    private void AssertNoTransactionArtifacts()
    {
        Assert.DoesNotContain(
            Directory.EnumerateFiles(testDirectory),
            path => path.Contains(".stage-", StringComparison.Ordinal)
                || path.Contains(".backup-", StringComparison.Ordinal));
    }

    private sealed record Fixture(
        PythonCredentialProvisioningRequest Request,
        string SourceProfile,
        string TrustedServer,
        string Certificate,
        string PrivateKey,
        string Profile);
}
