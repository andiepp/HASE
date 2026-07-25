namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostCertificateAuthenticationResultTests
{
    [Fact]
    public void Authenticated_ShouldPreservePrincipal()
    {
        RuntimeHostClientPrincipal principal =
            CreatePrincipal();

        RuntimeHostCertificateAuthenticationResult result =
            RuntimeHostCertificateAuthenticationResult.Authenticated(
                principal);

        Assert.True(
            result.IsAuthenticated);
        Assert.Same(
            principal,
            result.Principal);
        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason.None,
            result.FailureReason);
    }

    [Fact]
    public void CertificateInvalid_ShouldPreserveDetailedReason()
    {
        RuntimeHostCertificateAuthenticationResult result =
            RuntimeHostCertificateAuthenticationResult.CertificateInvalid(
                RuntimeHostClientCertificateValidationFailureReason
                    .CertificateExpired);

        Assert.False(
            result.IsAuthenticated);
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
    public void CertificateUntrusted_ShouldPreserveDetailedReason()
    {
        RuntimeHostCertificateAuthenticationResult result =
            RuntimeHostCertificateAuthenticationResult.CertificateUntrusted(
                RuntimeHostCertificateTrustFailureReason.ChainNotTrusted);

        Assert.False(
            result.IsAuthenticated);
        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .CertificateUntrusted,
            result.FailureReason);
        Assert.Equal(
            RuntimeHostCertificateTrustFailureReason.ChainNotTrusted,
            result.TrustFailureReason);
    }

    [Fact]
    public void UnknownCredential_ShouldCreateFailClosedResult()
    {
        RuntimeHostCertificateAuthenticationResult result =
            RuntimeHostCertificateAuthenticationResult.UnknownCredential();

        Assert.False(
            result.IsAuthenticated);
        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .UnknownCredential,
            result.FailureReason);
    }

    private static RuntimeHostClientPrincipal CreatePrincipal()
    {
        return new RuntimeHostClientPrincipal(
            "client-01",
            "certificate-01",
            "mutual-tls",
            new DateTimeOffset(
                2026,
                7,
                25,
                23,
                0,
                0,
                TimeSpan.Zero),
            "trust-v1");
    }
}
