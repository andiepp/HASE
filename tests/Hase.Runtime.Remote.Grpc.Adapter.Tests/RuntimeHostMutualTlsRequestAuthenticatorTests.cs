using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostMutualTlsRequestAuthenticatorTests
{
    private static readonly DateTimeOffset AuthenticationTimeUtc =
        new(
            2026,
            7,
            26,
            7,
            30,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Authenticate_AcceptedCertificate_ShouldProjectHttpContextUser()
    {
        RuntimeHostClientPrincipal principal =
            CreatePrincipal();
        DefaultHttpContext httpContext =
            new();
        RuntimeHostMutualTlsRequestAuthenticator authenticator =
            CreateAuthenticator(
                RuntimeHostCertificateAuthenticationResult.Authenticated(
                    principal));

        RuntimeHostMutualTlsClientCertificateAuthenticationResult result =
            authenticator.Authenticate(
                httpContext,
                null,
                AuthenticationTimeUtc);

        Assert.True(
            result.IsAccepted);
        Assert.True(
            httpContext.User.Identity?.IsAuthenticated);
        Assert.Equal(
            "client-01",
            httpContext.User.Identity?.Name);
    }

    [Fact]
    public void Authenticate_AcceptedCertificate_ShouldReturnSamePrincipal()
    {
        RuntimeHostClientPrincipal principal =
            CreatePrincipal();
        RuntimeHostMutualTlsRequestAuthenticator authenticator =
            CreateAuthenticator(
                RuntimeHostCertificateAuthenticationResult.Authenticated(
                    principal));

        RuntimeHostMutualTlsClientCertificateAuthenticationResult result =
            authenticator.Authenticate(
                new DefaultHttpContext(),
                null,
                AuthenticationTimeUtc);

        Assert.Same(
            principal,
            result.Principal);
    }

    [Fact]
    public void Authenticate_RejectedCertificate_ShouldLeaveUserUnchanged()
    {
        ClaimsPrincipal existingUser =
            new(
                new ClaimsIdentity());
        DefaultHttpContext httpContext =
            new()
            {
                User = existingUser
            };
        RuntimeHostMutualTlsRequestAuthenticator authenticator =
            CreateAuthenticator(
                RuntimeHostCertificateAuthenticationResult
                    .CertificateInvalid(
                        RuntimeHostClientCertificateValidationFailureReason
                            .CertificateExpired));

        RuntimeHostMutualTlsClientCertificateAuthenticationResult result =
            authenticator.Authenticate(
                httpContext,
                null,
                AuthenticationTimeUtc);

        Assert.False(
            result.IsAccepted);
        Assert.Same(
            existingUser,
            httpContext.User);
    }

    [Fact]
    public void Authenticate_UntrustedCertificate_ShouldPreserveFailure()
    {
        RuntimeHostMutualTlsRequestAuthenticator authenticator =
            CreateAuthenticator(
                RuntimeHostCertificateAuthenticationResult
                    .CertificateUntrusted(
                        RuntimeHostCertificateTrustFailureReason
                            .ChainNotTrusted));

        RuntimeHostMutualTlsClientCertificateAuthenticationResult result =
            authenticator.Authenticate(
                new DefaultHttpContext(),
                null,
                AuthenticationTimeUtc);

        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .CertificateUntrusted,
            result.FailureReason);
        Assert.Equal(
            RuntimeHostCertificateTrustFailureReason.ChainNotTrusted,
            result.TrustFailureReason);
    }

    [Fact]
    public void Authenticate_UnknownCredential_ShouldPreserveFailure()
    {
        RuntimeHostMutualTlsRequestAuthenticator authenticator =
            CreateAuthenticator(
                RuntimeHostCertificateAuthenticationResult.UnknownCredential());

        RuntimeHostMutualTlsClientCertificateAuthenticationResult result =
            authenticator.Authenticate(
                new DefaultHttpContext(),
                null,
                AuthenticationTimeUtc);

        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .UnknownCredential,
            result.FailureReason);
    }

    [Fact]
    public void Authenticate_NonUtcTimestamp_ShouldLeaveUserUnchanged()
    {
        ClaimsPrincipal existingUser =
            new(
                new ClaimsIdentity());
        DefaultHttpContext httpContext =
            new()
            {
                User = existingUser
            };
        RuntimeHostMutualTlsRequestAuthenticator authenticator =
            CreateAuthenticator(
                RuntimeHostCertificateAuthenticationResult.UnknownCredential());

        Assert.Throws<ArgumentException>(
            () => authenticator.Authenticate(
                httpContext,
                null,
                AuthenticationTimeUtc.ToOffset(
                    TimeSpan.FromHours(
                        2))));

        Assert.Same(
            existingUser,
            httpContext.User);
    }

    [Fact]
    public void Authenticate_MissingHttpContext_ShouldReject()
    {
        RuntimeHostMutualTlsRequestAuthenticator authenticator =
            CreateAuthenticator(
                RuntimeHostCertificateAuthenticationResult.UnknownCredential());

        Assert.Throws<ArgumentNullException>(
            () => authenticator.Authenticate(
                null!,
                null,
                AuthenticationTimeUtc));
    }

    [Fact]
    public void Constructor_MissingCertificateAuthenticator_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostMutualTlsRequestAuthenticator(
                null!,
                new RuntimeHostHttpContextIdentityProjector()));
    }

    [Fact]
    public void Constructor_MissingIdentityProjector_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostMutualTlsRequestAuthenticator(
                CreateCertificateAuthenticator(
                    RuntimeHostCertificateAuthenticationResult
                        .UnknownCredential()),
                null!));
    }

    private static RuntimeHostMutualTlsRequestAuthenticator
        CreateAuthenticator(
            RuntimeHostCertificateAuthenticationResult result)
    {
        return new RuntimeHostMutualTlsRequestAuthenticator(
            CreateCertificateAuthenticator(
                result),
            new RuntimeHostHttpContextIdentityProjector());
    }

    private static RuntimeHostMutualTlsClientCertificateAuthenticator
        CreateCertificateAuthenticator(
            RuntimeHostCertificateAuthenticationResult result)
    {
        return new RuntimeHostMutualTlsClientCertificateAuthenticator(
            new StubAuthenticationService(
                result));
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
}
