using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostHttpContextIdentityProjectorTests
{
    private static readonly DateTimeOffset AuthenticationTimeUtc =
        new(
            2026,
            7,
            26,
            7,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Project_ShouldAssignAuthenticatedUser()
    {
        DefaultHttpContext httpContext =
            new();
        RuntimeHostHttpContextIdentityProjector projector =
            new();

        projector.Project(
            httpContext,
            CreatePrincipal());

        Assert.True(
            httpContext.User.Identity?.IsAuthenticated);
        Assert.Equal(
            "client-01",
            httpContext.User.Identity?.Name);
    }

    [Fact]
    public void Project_ShouldUseMutualTlsAuthenticationScheme()
    {
        DefaultHttpContext httpContext =
            new();
        RuntimeHostHttpContextIdentityProjector projector =
            new();

        projector.Project(
            httpContext,
            CreatePrincipal());

        Assert.Equal(
            RuntimeHostMutualTlsAuthenticationDefaults.AuthenticationScheme,
            httpContext.User.Identity?.AuthenticationType);
    }

    [Fact]
    public void Project_ShouldPreserveAllProjectedClaims()
    {
        DefaultHttpContext httpContext =
            new();
        RuntimeHostHttpContextIdentityProjector projector =
            new();

        projector.Project(
            httpContext,
            CreatePrincipal());

        ClaimsIdentity identity =
            Assert.IsType<ClaimsIdentity>(
                httpContext.User.Identity);

        Assert.Equal(
            5,
            identity.Claims.Count());
        Assert.Equal(
            "certificate-01",
            httpContext.User.FindFirstValue(
                RuntimeHostClientClaimTypes.CredentialId));
        Assert.Equal(
            "mutual-tls",
            httpContext.User.FindFirstValue(
                RuntimeHostClientClaimTypes.AuthenticationMechanism));
        Assert.Equal(
            "trust-v1",
            httpContext.User.FindFirstValue(
                RuntimeHostClientClaimTypes.TrustPolicyId));
    }

    [Fact]
    public void Project_ShouldReplaceExistingAnonymousUser()
    {
        DefaultHttpContext httpContext =
            new();
        httpContext.User =
            new ClaimsPrincipal(
                new ClaimsIdentity());
        RuntimeHostHttpContextIdentityProjector projector =
            new();

        projector.Project(
            httpContext,
            CreatePrincipal());

        Assert.True(
            httpContext.User.Identity?.IsAuthenticated);
        Assert.Single(
            httpContext.User.Identities);
    }

    [Fact]
    public void Project_ShouldNotChangeRequestMethod()
    {
        DefaultHttpContext httpContext =
            new();
        httpContext.Request.Method =
            HttpMethods.Post;
        RuntimeHostHttpContextIdentityProjector projector =
            new();

        projector.Project(
            httpContext,
            CreatePrincipal());

        Assert.Equal(
            HttpMethods.Post,
            httpContext.Request.Method);
    }

    [Fact]
    public void Project_ShouldNotChangeRequestPath()
    {
        DefaultHttpContext httpContext =
            new();
        httpContext.Request.Path =
            "/hase.runtime.v1.RuntimeHost/Snapshot";
        RuntimeHostHttpContextIdentityProjector projector =
            new();

        projector.Project(
            httpContext,
            CreatePrincipal());

        Assert.Equal(
            "/hase.runtime.v1.RuntimeHost/Snapshot",
            httpContext.Request.Path);
    }

    [Fact]
    public void Project_MissingHttpContext_ShouldReject()
    {
        RuntimeHostHttpContextIdentityProjector projector =
            new();

        Assert.Throws<ArgumentNullException>(
            () => projector.Project(
                null!,
                CreatePrincipal()));
    }

    [Fact]
    public void Project_MissingPrincipal_ShouldReject()
    {
        RuntimeHostHttpContextIdentityProjector projector =
            new();

        Assert.Throws<ArgumentNullException>(
            () => projector.Project(
                new DefaultHttpContext(),
                null!));
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
}
