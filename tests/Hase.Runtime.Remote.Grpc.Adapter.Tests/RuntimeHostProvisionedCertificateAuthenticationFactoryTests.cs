using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class
    RuntimeHostProvisionedCertificateAuthenticationFactoryTests
{
    private static readonly DateTimeOffset AuthenticationTimeUtc =
        new(
            2026,
            7,
            26,
            12,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_MissingTrustEvaluator_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            "trustEvaluator",
            () =>
                RuntimeHostProvisionedCertificateAuthenticationFactory
                    .CreateAsync(
                        "unused",
                        null!));
    }

    [Fact]
    public async Task CreateAsync_EnrolledCertificate_ShouldAuthenticate()
    {
        using X509Certificate2 certificate =
            CreateClientCertificate();
        using var enrollmentFile =
            await EnrollmentFile.CreateAsync(
                certificate);

        IRuntimeHostCertificateAuthenticationService authenticationService =
            await RuntimeHostProvisionedCertificateAuthenticationFactory
                .CreateAsync(
                    enrollmentFile.FilePath,
                    new FixedTrustEvaluator(
                        trusted: true));

        RuntimeHostCertificateAuthenticationResult result =
            authenticationService.Authenticate(
                certificate,
                AuthenticationTimeUtc);

        Assert.True(
            result.IsAuthenticated);
        Assert.NotNull(
            result.Principal);
        Assert.Equal(
            "remote-client",
            result.Principal.PrincipalId);
        Assert.Equal(
            "private-network-trust-v1",
            result.Principal.TrustPolicyId);
    }

    [Fact]
    public async Task CreateAsync_UnenrolledCertificate_ShouldReject()
    {
        using X509Certificate2 enrolledCertificate =
            CreateClientCertificate();
        using X509Certificate2 presentedCertificate =
            CreateClientCertificate();
        using var enrollmentFile =
            await EnrollmentFile.CreateAsync(
                enrolledCertificate);

        IRuntimeHostCertificateAuthenticationService authenticationService =
            await RuntimeHostProvisionedCertificateAuthenticationFactory
                .CreateAsync(
                    enrollmentFile.FilePath,
                    new FixedTrustEvaluator(
                        trusted: true));

        RuntimeHostCertificateAuthenticationResult result =
            authenticationService.Authenticate(
                presentedCertificate,
                AuthenticationTimeUtc);

        Assert.False(
            result.IsAuthenticated);
        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .UnknownCredential,
            result.FailureReason);
    }

    [Fact]
    public async Task CreateAsync_UntrustedEnrolledCertificate_ShouldReject()
    {
        using X509Certificate2 certificate =
            CreateClientCertificate();
        using var enrollmentFile =
            await EnrollmentFile.CreateAsync(
                certificate);

        IRuntimeHostCertificateAuthenticationService authenticationService =
            await RuntimeHostProvisionedCertificateAuthenticationFactory
                .CreateAsync(
                    enrollmentFile.FilePath,
                    new FixedTrustEvaluator(
                        trusted: false));

        RuntimeHostCertificateAuthenticationResult result =
            authenticationService.Authenticate(
                certificate,
                AuthenticationTimeUtc);

        Assert.False(
            result.IsAuthenticated);
        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .CertificateUntrusted,
            result.FailureReason);
    }

    [Fact]
    public async Task CreateAsync_PreCancelled_ShouldThrow()
    {
        using X509Certificate2 certificate =
            CreateClientCertificate();
        using var enrollmentFile =
            await EnrollmentFile.CreateAsync(
                certificate);
        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                RuntimeHostProvisionedCertificateAuthenticationFactory
                    .CreateAsync(
                        enrollmentFile.FilePath,
                        new FixedTrustEvaluator(
                            trusted: true),
                        cancellationSource.Token));
    }

    private static X509Certificate2 CreateClientCertificate()
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            new(
                "CN=HASE generated provisioned-client test",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new(
                        "1.3.6.1.5.5.7.3.2")
                },
                true));

        return request.CreateSelfSigned(
            AuthenticationTimeUtc.AddDays(
                -1),
            AuthenticationTimeUtc.AddDays(
                1));
    }

    private sealed class FixedTrustEvaluator
        : IRuntimeHostCertificateTrustEvaluator
    {
        private readonly bool trusted;

        public FixedTrustEvaluator(
            bool trusted)
        {
            this.trusted =
                trusted;
        }

        public bool IsTrusted(
            X509Certificate2 certificate,
            DateTimeOffset validationTimeUtc)
        {
            return trusted;
        }
    }

    private sealed class EnrollmentFile
        : IDisposable
    {
        private EnrollmentFile(
            string directoryPath,
            string filePath)
        {
            DirectoryPath =
                directoryPath;
            FilePath =
                filePath;
        }

        public string DirectoryPath
        {
            get;
        }

        public string FilePath
        {
            get;
        }

        public static async Task<EnrollmentFile> CreateAsync(
            X509Certificate2 certificate)
        {
            RuntimeHostClientCredentialIdentity credentialIdentity =
                new RuntimeHostX509ClientCredentialIdentityExtractor()
                    .Extract(
                        certificate);
            string directoryPath =
                Path.Combine(
                    Path.GetTempPath(),
                    $"hase-provisioned-authentication-{Guid.NewGuid():N}");
            Directory.CreateDirectory(
                directoryPath);
            string filePath =
                Path.Combine(
                    directoryPath,
                    "client-enrollments.json");
            var document =
                new
                {
                    formatVersion =
                        1,
                    enrollments =
                        new[]
                        {
                            new
                            {
                                credentialId =
                                    credentialIdentity.CredentialId.Value,
                                principalId =
                                    "remote-client",
                                trustPolicyId =
                                    "private-network-trust-v1"
                            }
                        }
                };

            await File.WriteAllTextAsync(
                filePath,
                JsonSerializer.Serialize(
                    document));

            return new EnrollmentFile(
                directoryPath,
                filePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(
                    DirectoryPath))
            {
                Directory.Delete(
                    DirectoryPath,
                    recursive: true);
            }
        }
    }
}
