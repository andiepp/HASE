using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostMutualTlsClientCertificateAuthenticatorTests
{
    private static readonly DateTimeOffset AuthenticationTimeUtc =
        new(
            2026,
            7,
            26,
            6,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Authenticate_Success_ShouldAcceptAndPreservePrincipal()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedCertificate();
        RuntimeHostClientPrincipal principal =
            CreatePrincipal();
        RuntimeHostMutualTlsClientCertificateAuthenticator authenticator =
            new(
                new StubAuthenticationService(
                    RuntimeHostCertificateAuthenticationResult.Authenticated(
                        principal)));

        RuntimeHostMutualTlsClientCertificateAuthenticationResult result =
            authenticator.Authenticate(
                certificate,
                AuthenticationTimeUtc);

        Assert.True(
            result.IsAccepted);
        Assert.Same(
            principal,
            result.Principal);
        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason.None,
            result.FailureReason);
    }

    [Fact]
    public void Authenticate_InvalidCertificate_ShouldRejectAndPreserveReason()
    {
        RuntimeHostMutualTlsClientCertificateAuthenticator authenticator =
            new(
                new StubAuthenticationService(
                    RuntimeHostCertificateAuthenticationResult
                        .CertificateInvalid(
                            RuntimeHostClientCertificateValidationFailureReason
                                .CertificateExpired)));

        RuntimeHostMutualTlsClientCertificateAuthenticationResult result =
            authenticator.Authenticate(
                null,
                AuthenticationTimeUtc);

        Assert.False(
            result.IsAccepted);
        Assert.Null(
            result.Principal);
        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .CertificateInvalid,
            result.FailureReason);
        Assert.Equal(
            RuntimeHostClientCertificateValidationFailureReason
                .CertificateExpired,
            result.CertificateValidationFailureReason);
    }

    [Fact]
    public void Authenticate_UntrustedCertificate_ShouldRejectAndPreserveReason()
    {
        RuntimeHostMutualTlsClientCertificateAuthenticator authenticator =
            new(
                new StubAuthenticationService(
                    RuntimeHostCertificateAuthenticationResult
                        .CertificateUntrusted(
                            RuntimeHostCertificateTrustFailureReason
                                .ChainNotTrusted)));

        RuntimeHostMutualTlsClientCertificateAuthenticationResult result =
            authenticator.Authenticate(
                null,
                AuthenticationTimeUtc);

        Assert.False(
            result.IsAccepted);
        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .CertificateUntrusted,
            result.FailureReason);
        Assert.Equal(
            RuntimeHostCertificateTrustFailureReason.ChainNotTrusted,
            result.TrustFailureReason);
    }

    [Fact]
    public void Authenticate_UnknownCredential_ShouldReject()
    {
        RuntimeHostMutualTlsClientCertificateAuthenticator authenticator =
            new(
                new StubAuthenticationService(
                    RuntimeHostCertificateAuthenticationResult
                        .UnknownCredential()));

        RuntimeHostMutualTlsClientCertificateAuthenticationResult result =
            authenticator.Authenticate(
                null,
                AuthenticationTimeUtc);

        Assert.False(
            result.IsAccepted);
        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .UnknownCredential,
            result.FailureReason);
    }

    [Fact]
    public void Authenticate_NonUtcTimestamp_ShouldRejectBeforeService()
    {
        TrackingAuthenticationService service =
            new(
                RuntimeHostCertificateAuthenticationResult.UnknownCredential());
        RuntimeHostMutualTlsClientCertificateAuthenticator authenticator =
            new(
                service);

        Assert.Throws<ArgumentException>(
            () => authenticator.Authenticate(
                null,
                AuthenticationTimeUtc.ToOffset(
                    TimeSpan.FromHours(
                        2))));

        Assert.False(
            service.WasCalled);
    }

    [Fact]
    public void Authenticate_NullServiceResult_ShouldReject()
    {
        RuntimeHostMutualTlsClientCertificateAuthenticator authenticator =
            new(
                new NullAuthenticationService());

        Assert.Throws<InvalidOperationException>(
            () => authenticator.Authenticate(
                null,
                AuthenticationTimeUtc));
    }

    [Fact]
    public void Constructor_MissingService_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostMutualTlsClientCertificateAuthenticator(
                null!));
    }

    [Fact]
    public void Accepted_MissingPrincipal_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                RuntimeHostMutualTlsClientCertificateAuthenticationResult
                    .Accepted(
                        null!));
    }

    [Fact]
    public void Rejected_MissingFailureReason_ShouldReject()
    {
        Assert.Throws<ArgumentException>(
            () =>
                RuntimeHostMutualTlsClientCertificateAuthenticationResult
                    .Rejected(
                        RuntimeHostCertificateAuthenticationFailureReason.None,
                        RuntimeHostClientCertificateValidationFailureReason
                            .None,
                        RuntimeHostCertificateTrustFailureReason.None));
    }

    private static RuntimeHostClientPrincipal CreatePrincipal()
    {
        return new RuntimeHostClientPrincipal(
            "client-01",
            "certificate-01",
            "mutual-tls",
            AuthenticationTimeUtc,
            "trust-v1");
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            new(
                "CN=hase-client",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(
            AuthenticationTimeUtc.AddDays(
                -1),
            AuthenticationTimeUtc.AddDays(
                1));
    }

    private sealed class StubAuthenticationService
        : IRuntimeHostCertificateAuthenticationService
    {
        private readonly RuntimeHostCertificateAuthenticationResult result;

        public StubAuthenticationService(
            RuntimeHostCertificateAuthenticationResult result)
        {
            this.result = result;
        }

        public RuntimeHostCertificateAuthenticationResult Authenticate(
            X509Certificate2? certificate,
            DateTimeOffset authenticatedAtUtc)
        {
            return result;
        }
    }

    private sealed class TrackingAuthenticationService
        : IRuntimeHostCertificateAuthenticationService
    {
        private readonly RuntimeHostCertificateAuthenticationResult result;

        public TrackingAuthenticationService(
            RuntimeHostCertificateAuthenticationResult result)
        {
            this.result = result;
        }

        public bool WasCalled { get; private set; }

        public RuntimeHostCertificateAuthenticationResult Authenticate(
            X509Certificate2? certificate,
            DateTimeOffset authenticatedAtUtc)
        {
            WasCalled = true;
            return result;
        }
    }

    private sealed class NullAuthenticationService
        : IRuntimeHostCertificateAuthenticationService
    {
        public RuntimeHostCertificateAuthenticationResult Authenticate(
            X509Certificate2? certificate,
            DateTimeOffset authenticatedAtUtc)
        {
            return null!;
        }
    }
}
