using Hase.Python.CredentialProvisioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

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
            root, material, request, privateKey, expectedGrants);
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
