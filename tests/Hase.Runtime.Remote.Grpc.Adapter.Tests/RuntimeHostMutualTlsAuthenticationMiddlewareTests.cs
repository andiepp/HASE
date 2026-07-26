using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostMutualTlsAuthenticationMiddlewareTests
{
    private static readonly DateTimeOffset AuthenticationTimeUtc =
        new(
            2026,
            7,
            26,
            8,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task InvokeAsync_AcceptedCertificate_ShouldCallNext()
    {
        bool nextCalled =
            false;
        RuntimeHostMutualTlsAuthenticationMiddleware middleware =
            CreateMiddleware(
                RuntimeHostCertificateAuthenticationResult.Authenticated(
                    CreatePrincipal()),
                _ =>
                {
                    nextCalled = true;
                    return Task.CompletedTask;
                });
        DefaultHttpContext httpContext =
            CreateHttpContextWithCertificate();

        await middleware.InvokeAsync(
            httpContext);

        Assert.True(
            nextCalled);
        Assert.Equal(
            StatusCodes.Status200OK,
            httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AcceptedCertificate_ShouldProjectUser()
    {
        RuntimeHostMutualTlsAuthenticationMiddleware middleware =
            CreateMiddleware(
                RuntimeHostCertificateAuthenticationResult.Authenticated(
                    CreatePrincipal()));
        DefaultHttpContext httpContext =
            CreateHttpContextWithCertificate();

        await middleware.InvokeAsync(
            httpContext);

        Assert.True(
            httpContext.User.Identity?.IsAuthenticated);
        Assert.Equal(
            "client-01",
            httpContext.User.Identity?.Name);
    }

    [Fact]
    public async Task InvokeAsync_RejectedCertificate_ShouldNotCallNext()
    {
        bool nextCalled =
            false;
        RuntimeHostMutualTlsAuthenticationMiddleware middleware =
            CreateMiddleware(
                RuntimeHostCertificateAuthenticationResult.UnknownCredential(),
                _ =>
                {
                    nextCalled = true;
                    return Task.CompletedTask;
                });
        DefaultHttpContext httpContext =
            CreateHttpContextWithCertificate();

        await middleware.InvokeAsync(
            httpContext);

        Assert.False(
            nextCalled);
        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_MissingCertificate_ShouldReject()
    {
        RuntimeHostMutualTlsAuthenticationMiddleware middleware =
            CreateMiddleware(
                RuntimeHostCertificateAuthenticationResult.CertificateInvalid(
                    RuntimeHostClientCertificateValidationFailureReason
                        .CertificateMissing));
        DefaultHttpContext httpContext =
            new();

        await middleware.InvokeAsync(
            httpContext);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ShouldStoreAuthenticationResult()
    {
        RuntimeHostMutualTlsAuthenticationMiddleware middleware =
            CreateMiddleware(
                RuntimeHostCertificateAuthenticationResult
                    .CertificateUntrusted(
                        RuntimeHostCertificateTrustFailureReason
                            .ChainNotTrusted));
        DefaultHttpContext httpContext =
            CreateHttpContextWithCertificate();

        await middleware.InvokeAsync(
            httpContext);

        RuntimeHostMutualTlsClientCertificateAuthenticationResult result =
            Assert.IsType<
                RuntimeHostMutualTlsClientCertificateAuthenticationResult>(
                    httpContext.Items[
                        RuntimeHostMutualTlsHttpContextItems
                            .AuthenticationResult]);

        Assert.Equal(
            RuntimeHostCertificateAuthenticationFailureReason
                .CertificateUntrusted,
            result.FailureReason);
    }

    [Fact]
    public async Task InvokeAsync_ShouldUseUtcTimeProviderValue()
    {
        TrackingAuthenticationService service =
            new(
                RuntimeHostCertificateAuthenticationResult.UnknownCredential());
        RuntimeHostMutualTlsAuthenticationMiddleware middleware =
            CreateMiddleware(
                service);
        DefaultHttpContext httpContext =
            CreateHttpContextWithCertificate();

        await middleware.InvokeAsync(
            httpContext);

        Assert.Equal(
            AuthenticationTimeUtc,
            service.AuthenticatedAtUtc);
    }

    [Fact]
    public async Task InvokeAsync_MissingHttpContext_ShouldReject()
    {
        RuntimeHostMutualTlsAuthenticationMiddleware middleware =
            CreateMiddleware(
                RuntimeHostCertificateAuthenticationResult.UnknownCredential());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => middleware.InvokeAsync(
                null!));
    }

    [Fact]
    public void Constructor_MissingNext_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostMutualTlsAuthenticationMiddleware(
                null!,
                CreateRequestAuthenticator(
                    new StubAuthenticationService(
                        RuntimeHostCertificateAuthenticationResult
                            .UnknownCredential())),
                new FixedTimeProvider(
                    AuthenticationTimeUtc)));
    }

    [Fact]
    public void Constructor_MissingRequestAuthenticator_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostMutualTlsAuthenticationMiddleware(
                _ => Task.CompletedTask,
                null!,
                new FixedTimeProvider(
                    AuthenticationTimeUtc)));
    }

    [Fact]
    public void Constructor_MissingTimeProvider_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostMutualTlsAuthenticationMiddleware(
                _ => Task.CompletedTask,
                CreateRequestAuthenticator(
                    new StubAuthenticationService(
                        RuntimeHostCertificateAuthenticationResult
                            .UnknownCredential())),
                null!));
    }

    private static RuntimeHostMutualTlsAuthenticationMiddleware
        CreateMiddleware(
            RuntimeHostCertificateAuthenticationResult result,
            RequestDelegate? next = null)
    {
        return CreateMiddleware(
            new StubAuthenticationService(
                result),
            next);
    }

    private static RuntimeHostMutualTlsAuthenticationMiddleware
        CreateMiddleware(
            IRuntimeHostCertificateAuthenticationService service,
            RequestDelegate? next = null)
    {
        return new RuntimeHostMutualTlsAuthenticationMiddleware(
            next
                ?? (_ => Task.CompletedTask),
            CreateRequestAuthenticator(
                service),
            new FixedTimeProvider(
                AuthenticationTimeUtc));
    }

    private static RuntimeHostMutualTlsRequestAuthenticator
        CreateRequestAuthenticator(
            IRuntimeHostCertificateAuthenticationService service)
    {
        return new RuntimeHostMutualTlsRequestAuthenticator(
            new RuntimeHostMutualTlsClientCertificateAuthenticator(
                service),
            new RuntimeHostHttpContextIdentityProjector());
    }

    private static DefaultHttpContext CreateHttpContextWithCertificate()
    {
        DefaultHttpContext httpContext =
            new();
        httpContext.Connection.ClientCertificate =
            CreateSelfSignedCertificate();

        return httpContext;
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

    private sealed class TrackingAuthenticationService
        : IRuntimeHostCertificateAuthenticationService
    {
        private readonly RuntimeHostCertificateAuthenticationResult result;

        public TrackingAuthenticationService(
            RuntimeHostCertificateAuthenticationResult result)
        {
            this.result = result;
        }

        public DateTimeOffset? AuthenticatedAtUtc { get; private set; }

        public RuntimeHostCertificateAuthenticationResult Authenticate(
            X509Certificate2? certificate,
            DateTimeOffset authenticatedAtUtc)
        {
            AuthenticatedAtUtc =
                authenticatedAtUtc;
            return result;
        }
    }

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        public FixedTimeProvider(
            DateTimeOffset utcNow)
        {
            this.utcNow =
                utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
