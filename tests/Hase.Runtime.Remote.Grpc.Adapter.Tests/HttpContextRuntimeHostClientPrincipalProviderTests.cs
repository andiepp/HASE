using System.Security.Claims;
using Grpc.Core;
using Microsoft.AspNetCore.Http;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class HttpContextRuntimeHostClientPrincipalProviderTests
{
    [Fact]
    public void Constructor_NullAccessor_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "httpContextAccessor",
            () => new HttpContextRuntimeHostClientPrincipalProvider(null!));
    }

    [Fact]
    public void GetPrincipal_NullCallContext_ShouldThrow()
    {
        var provider = CreateProvider(CreateClaimsPrincipal());

        Assert.Throws<ArgumentNullException>(
            "context",
            () => provider.GetPrincipal(null));
    }

    [Fact]
    public void GetPrincipal_MissingHttpContext_ShouldThrow()
    {
        var provider = new HttpContextRuntimeHostClientPrincipalProvider(
            new HttpContextAccessor());

        Assert.Throws<InvalidOperationException>(() =>
            provider.GetPrincipal(new TestServerCallContext()));
    }

    [Fact]
    public void GetPrincipal_UnauthenticatedIdentity_ShouldThrow()
    {
        var provider = CreateProvider(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Throws<InvalidOperationException>(() =>
            provider.GetPrincipal(new TestServerCallContext()));
    }

    [Fact]
    public void GetPrincipal_CompleteAuthenticatedClaims_ShouldReconstructExactPrincipal()
    {
        RuntimeHostClientPrincipal expected = CreatePrincipal();
        var provider = CreateProvider(
            RuntimeHostClaimsPrincipalFactory.Create(expected));

        RuntimeHostClientPrincipal actual =
            provider.GetPrincipal(new TestServerCallContext());

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(RuntimeHostClientClaimTypes.PrincipalId)]
    [InlineData(RuntimeHostClientClaimTypes.CredentialId)]
    [InlineData(RuntimeHostClientClaimTypes.AuthenticationMechanism)]
    [InlineData(RuntimeHostClientClaimTypes.AuthenticatedAtUtc)]
    [InlineData(RuntimeHostClientClaimTypes.TrustPolicyId)]
    public void GetPrincipal_MissingRequiredClaim_ShouldThrow(string claimType)
    {
        ClaimsPrincipal principal = CreateClaimsPrincipal();
        ClaimsIdentity identity = Assert.IsType<ClaimsIdentity>(principal.Identity);
        identity.RemoveClaim(Assert.Single(identity.FindAll(claimType)));

        Assert.Throws<InvalidOperationException>(() =>
            CreateProvider(principal).GetPrincipal(new TestServerCallContext()));
    }

    [Fact]
    public void GetPrincipal_DuplicateRequiredClaim_ShouldThrow()
    {
        ClaimsPrincipal principal = CreateClaimsPrincipal();
        ClaimsIdentity identity = Assert.IsType<ClaimsIdentity>(principal.Identity);
        identity.AddClaim(new Claim(
            RuntimeHostClientClaimTypes.PrincipalId,
            "duplicate"));

        Assert.Throws<InvalidOperationException>(() =>
            CreateProvider(principal).GetPrincipal(new TestServerCallContext()));
    }

    [Fact]
    public void GetPrincipal_EmptyRequiredClaim_ShouldThrow()
    {
        ClaimsPrincipal principal = CreateClaimsPrincipal();
        ClaimsIdentity identity = Assert.IsType<ClaimsIdentity>(principal.Identity);
        Claim original = Assert.Single(identity.FindAll(
            RuntimeHostClientClaimTypes.TrustPolicyId));
        identity.RemoveClaim(original);
        identity.AddClaim(new Claim(
            RuntimeHostClientClaimTypes.TrustPolicyId,
            " "));

        Assert.Throws<InvalidOperationException>(() =>
            CreateProvider(principal).GetPrincipal(new TestServerCallContext()));
    }

    [Theory]
    [InlineData("not-a-timestamp")]
    [InlineData("2026-08-07T10:00:00.0000000+02:00")]
    public void GetPrincipal_InvalidUtcTimestamp_ShouldThrow(string value)
    {
        ClaimsPrincipal principal = CreateClaimsPrincipal();
        ClaimsIdentity identity = Assert.IsType<ClaimsIdentity>(principal.Identity);
        Claim original = Assert.Single(identity.FindAll(
            RuntimeHostClientClaimTypes.AuthenticatedAtUtc));
        identity.RemoveClaim(original);
        identity.AddClaim(new Claim(
            RuntimeHostClientClaimTypes.AuthenticatedAtUtc,
            value));

        Assert.Throws<InvalidOperationException>(() =>
            CreateProvider(principal).GetPrincipal(new TestServerCallContext()));
    }

    private static HttpContextRuntimeHostClientPrincipalProvider CreateProvider(
        ClaimsPrincipal principal)
    {
        var context = new DefaultHttpContext { User = principal };
        return new HttpContextRuntimeHostClientPrincipalProvider(
            new HttpContextAccessor { HttpContext = context });
    }

    private static ClaimsPrincipal CreateClaimsPrincipal() =>
        RuntimeHostClaimsPrincipalFactory.Create(CreatePrincipal());

    private static RuntimeHostClientPrincipal CreatePrincipal() =>
        new(
            "client-one",
            "credential-one",
            "mutual-tls",
            new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero),
            "trust-policy-one");

    private sealed class TestServerCallContext : ServerCallContext
    {
        protected override string MethodCore => "test";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "test";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => [];
        protected override CancellationToken CancellationTokenCore =>
            CancellationToken.None;
        protected override Metadata ResponseTrailersCore { get; } = [];
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => null!;
        protected override ContextPropagationToken CreatePropagationTokenCore(
            ContextPropagationOptions? options) => throw new NotSupportedException();
        protected override Task WriteResponseHeadersAsyncCore(
            Metadata responseHeaders) => Task.CompletedTask;
    }
}
