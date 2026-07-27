using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostClientCredentialEnrollmentProvisionerTests
{
    [Fact]
    public async Task CreateNewAsync_MissingCertificate_ShouldThrow()
    {
        using var directory =
            new TemporaryDirectory();

        await Assert.ThrowsAsync<ArgumentNullException>(
            "clientCertificate",
            () =>
                RuntimeHostClientCredentialEnrollmentProvisioner
                    .CreateNewAsync(
                        Path.Combine(
                            directory.Path,
                            "client-enrollments.json"),
                        null!,
                        new RuntimeHostClientPrincipalId(
                            "remote-client"),
                        "private-network-trust-v1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("client-enrollments.json")]
    public async Task CreateNewAsync_InvalidPath_ShouldThrow(
        string filePath)
    {
        using X509Certificate2 certificate =
            CreateCertificate();

        await Assert.ThrowsAsync<ArgumentException>(
            "filePath",
            () =>
                RuntimeHostClientCredentialEnrollmentProvisioner
                    .CreateNewAsync(
                        filePath,
                        certificate,
                        new RuntimeHostClientPrincipalId(
                            "remote-client"),
                        "private-network-trust-v1"));
    }

    [Fact]
    public async Task CreateNewAsync_MissingPrincipal_ShouldThrow()
    {
        using var directory =
            new TemporaryDirectory();
        using X509Certificate2 certificate =
            CreateCertificate();

        await Assert.ThrowsAsync<ArgumentException>(
            "principalId",
            () =>
                RuntimeHostClientCredentialEnrollmentProvisioner
                    .CreateNewAsync(
                        Path.Combine(
                            directory.Path,
                            "client-enrollments.json"),
                        certificate,
                        default,
                        "private-network-trust-v1"));
    }

    [Fact]
    public async Task CreateNewAsync_ExistingTarget_ShouldNotOverwrite()
    {
        using var directory =
            new TemporaryDirectory();
        using X509Certificate2 certificate =
            CreateCertificate();
        string filePath =
            Path.Combine(
                directory.Path,
                "client-enrollments.json");
        const string existingContents =
            "existing";
        await File.WriteAllTextAsync(
            filePath,
            existingContents);

        await Assert.ThrowsAsync<IOException>(
            () =>
                RuntimeHostClientCredentialEnrollmentProvisioner
                    .CreateNewAsync(
                        filePath,
                        certificate,
                        new RuntimeHostClientPrincipalId(
                            "remote-client"),
                        "private-network-trust-v1"));

        Assert.Equal(
            existingContents,
            await File.ReadAllTextAsync(
                filePath));
    }

    [Fact]
    public async Task CreateNewAsync_ValidCertificate_ShouldRoundTrip()
    {
        using var directory =
            new TemporaryDirectory();
        using X509Certificate2 certificate =
            CreateCertificate();
        string filePath =
            Path.Combine(
                directory.Path,
                "client-enrollments.json");

        await RuntimeHostClientCredentialEnrollmentProvisioner.CreateNewAsync(
            filePath,
            certificate,
            new RuntimeHostClientPrincipalId(
                "remote-client"),
            "private-network-trust-v1");

        RuntimeHostClientCredentialEnrollmentRegistry registry =
            await RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
                filePath);
        RuntimeHostClientCredentialIdentity credentialIdentity =
            new RuntimeHostX509ClientCredentialIdentityExtractor()
                .Extract(
                    certificate);
        bool resolved =
            registry.TryResolve(
                credentialIdentity,
                DateTimeOffset.UtcNow,
                out RuntimeHostClientPrincipal? principal);

        Assert.True(
            resolved);
        Assert.NotNull(
            principal);
        Assert.Equal(
            "remote-client",
            principal.PrincipalId);
        Assert.Equal(
            "private-network-trust-v1",
            principal.TrustPolicyId);
    }

    [Fact]
    public async Task CreateNewAsync_PreCancelled_ShouldNotCreateFile()
    {
        using var directory =
            new TemporaryDirectory();
        using X509Certificate2 certificate =
            CreateCertificate();
        string filePath =
            Path.Combine(
                directory.Path,
                "client-enrollments.json");
        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                RuntimeHostClientCredentialEnrollmentProvisioner
                    .CreateNewAsync(
                        filePath,
                        certificate,
                        new RuntimeHostClientPrincipalId(
                            "remote-client"),
                        "private-network-trust-v1",
                        cancellationSource.Token));

        Assert.False(
            File.Exists(
                filePath));
    }

    private static X509Certificate2 CreateCertificate()
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            new(
                "CN=HASE generated enrollment-provisioning test",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(
                -1),
            DateTimeOffset.UtcNow.AddMinutes(
                5));
    }

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"hase-enrollment-provisioning-{Guid.NewGuid():N}");

            Directory.CreateDirectory(
                Path);
        }

        public string Path
        {
            get;
        }

        public void Dispose()
        {
            if (Directory.Exists(
                    Path))
            {
                Directory.Delete(
                    Path,
                    recursive: true);
            }
        }
    }
}
