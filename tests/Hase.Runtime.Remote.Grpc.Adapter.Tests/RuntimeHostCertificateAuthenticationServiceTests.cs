using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostCertificateAuthenticationServiceTests
{
    private static readonly DateTimeOffset AuthenticationTimeUtc =
        new(
            2026,
            7,
            25,
            23,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Authenticate_LocallyInvalidCertificate_ShouldStopBeforeTrust()
    {
        TrackingTrustValidator trustValidator =
            new(
                RuntimeHostCertificateTrustValidationResult.Trusted());
        RuntimeHostCertificateAuthenticationService service =
            CreateService(
                new StubCertificateValidator(
                    RuntimeHostClientCertificateValidationResult.Invalid(
                        RuntimeHostClientCertificateValidationFailureReason
                            .CertificateExpired)),
                trustValidator);

        RuntimeHostCertificateAuthenticationResult result =
            service.Authenticate(
                null,
                AuthenticationTimeUtc);

        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .CertificateInvalid,
            result.FailureReason);
        Assert.False(
            trustValidator.WasCalled);
    }

    [Fact]
    public void Authenticate_UntrustedCertificate_ShouldStopBeforeExtraction()
    {
        using X509Certificate2 certificate =
            CreateCertificate();
        TrackingIdentityExtractor extractor =
            new();
        RuntimeHostCertificateAuthenticationService service =
            CreateService(
                new StubCertificateValidator(
                    RuntimeHostClientCertificateValidationResult.Valid()),
                new TrackingTrustValidator(
                    RuntimeHostCertificateTrustValidationResult.Untrusted(
                        RuntimeHostCertificateTrustFailureReason
                            .ChainNotTrusted)),
                extractor);

        RuntimeHostCertificateAuthenticationResult result =
            service.Authenticate(
                certificate,
                AuthenticationTimeUtc);

        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .CertificateUntrusted,
            result.FailureReason);
        Assert.False(
            extractor.WasCalled);
    }

    [Fact]
    public void Authenticate_UnknownCredential_ShouldFailClosed()
    {
        using X509Certificate2 certificate =
            CreateCertificate();
        RuntimeHostCertificateAuthenticationService service =
            CreateService(
                authenticationResult:
                    RuntimeHostAuthenticationResult.Failed(
                        RuntimeHostAuthenticationFailureReason
                            .UnknownCredential));

        RuntimeHostCertificateAuthenticationResult result =
            service.Authenticate(
                certificate,
                AuthenticationTimeUtc);

        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .UnknownCredential,
            result.FailureReason);
        Assert.Null(
            result.Principal);
    }

    [Fact]
    public void Authenticate_ValidTrustedEnrolledCertificate_ShouldSucceed()
    {
        using X509Certificate2 certificate =
            CreateCertificate();
        RuntimeHostClientPrincipal principal =
            new(
                "client-01",
                "certificate-01",
                "mutual-tls",
                AuthenticationTimeUtc,
                "trust-v1");
        RuntimeHostCertificateAuthenticationService service =
            CreateService(
                authenticationResult:
                    RuntimeHostAuthenticationResult.Authenticated(
                        principal));

        RuntimeHostCertificateAuthenticationResult result =
            service.Authenticate(
                certificate,
                AuthenticationTimeUtc);

        Assert.True(
            result.IsAuthenticated);
        Assert.Same(
            principal,
            result.Principal);
    }

    [Fact]
    public void Authenticate_NonUtcTime_ShouldThrowBeforeValidation()
    {
        TrackingCertificateValidator validator =
            new();
        RuntimeHostCertificateAuthenticationService service =
            CreateService(
                validator);
        DateTimeOffset nonUtcTime =
            new(
                2026,
                7,
                26,
                1,
                0,
                0,
                TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            "authenticatedAtUtc",
            () =>
                service.Authenticate(
                    null,
                    nonUtcTime));

        Assert.False(
            validator.WasCalled);
    }

    private static RuntimeHostCertificateAuthenticationService CreateService(
        IRuntimeHostClientCertificateValidator? certificateValidator = null,
        IRuntimeHostCertificateTrustValidator? trustValidator = null,
        IRuntimeHostX509ClientCredentialIdentityExtractor? extractor = null,
        RuntimeHostAuthenticationResult? authenticationResult = null)
    {
        return new RuntimeHostCertificateAuthenticationService(
            certificateValidator
                ?? new StubCertificateValidator(
                    RuntimeHostClientCertificateValidationResult.Valid()),
            trustValidator
                ?? new TrackingTrustValidator(
                    RuntimeHostCertificateTrustValidationResult.Trusted()),
            extractor
                ?? new TrackingIdentityExtractor(),
            new StubAuthenticationService(
                authenticationResult
                    ?? RuntimeHostAuthenticationResult.Failed(
                        RuntimeHostAuthenticationFailureReason
                            .UnknownCredential)));
    }

    private static X509Certificate2 CreateCertificate()
    {
        using RSA key =
            RSA.Create(
                2048);

        CertificateRequest request =
            new(
                "CN=hase-client",
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(
            AuthenticationTimeUtc.AddDays(-1),
            AuthenticationTimeUtc.AddDays(1));
    }

    private sealed class StubCertificateValidator
        : IRuntimeHostClientCertificateValidator
    {
        private readonly RuntimeHostClientCertificateValidationResult result;

        public StubCertificateValidator(
            RuntimeHostClientCertificateValidationResult result)
        {
            this.result = result;
        }

        public RuntimeHostClientCertificateValidationResult Validate(
            X509Certificate2? certificate,
            DateTimeOffset validationTimeUtc)
        {
            return result;
        }
    }

    private sealed class TrackingCertificateValidator
        : IRuntimeHostClientCertificateValidator
    {
        public bool WasCalled { get; private set; }

        public RuntimeHostClientCertificateValidationResult Validate(
            X509Certificate2? certificate,
            DateTimeOffset validationTimeUtc)
        {
            WasCalled = true;
            return RuntimeHostClientCertificateValidationResult.Valid();
        }
    }

    private sealed class TrackingTrustValidator
        : IRuntimeHostCertificateTrustValidator
    {
        private readonly RuntimeHostCertificateTrustValidationResult result;

        public TrackingTrustValidator(
            RuntimeHostCertificateTrustValidationResult result)
        {
            this.result = result;
        }

        public bool WasCalled { get; private set; }

        public RuntimeHostCertificateTrustValidationResult Validate(
            X509Certificate2? certificate,
            DateTimeOffset validationTimeUtc)
        {
            WasCalled = true;
            return result;
        }
    }

    private sealed class TrackingIdentityExtractor
        : IRuntimeHostX509ClientCredentialIdentityExtractor
    {
        public bool WasCalled { get; private set; }

        public RuntimeHostClientCredentialIdentity Extract(
            X509Certificate2 certificate)
        {
            WasCalled = true;
            return new RuntimeHostClientCredentialIdentity(
                RuntimeHostAuthenticationMechanism.MutualTls,
                new RuntimeHostClientCredentialId(
                    "certificate-01"));
        }
    }

    private sealed class StubAuthenticationService
        : IRuntimeHostClientAuthenticationService
    {
        private readonly RuntimeHostAuthenticationResult result;

        public StubAuthenticationService(
            RuntimeHostAuthenticationResult result)
        {
            this.result = result;
        }

        public RuntimeHostAuthenticationResult Authenticate(
            RuntimeHostClientCredentialIdentity credentialIdentity,
            DateTimeOffset authenticatedAtUtc)
        {
            return result;
        }
    }
}
