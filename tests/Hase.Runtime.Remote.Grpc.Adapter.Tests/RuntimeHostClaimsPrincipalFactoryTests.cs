using System.Security.Claims;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostClaimsPrincipalFactoryTests
{
    private static readonly DateTimeOffset AuthenticationTimeUtc =
        new(
            2026,
            7,
            26,
            6,
            30,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Create_ShouldCreateAuthenticatedIdentity()
    {
        ClaimsPrincipal claimsPrincipal =
            RuntimeHostClaimsPrincipalFactory.Create(
                CreatePrincipal());

        ClaimsIdentity identity =
            Assert.IsType<ClaimsIdentity>(
                claimsPrincipal.Identity);

        Assert.True(
            identity.IsAuthenticated);
        Assert.Equal(
            RuntimeHostMutualTlsAuthenticationDefaults.AuthenticationScheme,
            identity.AuthenticationType);
    }

    [Fact]
    public void Create_ShouldUsePrincipalIdAsName()
    {
        ClaimsPrincipal claimsPrincipal =
            RuntimeHostClaimsPrincipalFactory.Create(
                CreatePrincipal());

        Assert.Equal(
            "client-01",
            claimsPrincipal.Identity?.Name);
    }

    [Fact]
    public void Create_ShouldProjectPrincipalId()
    {
        ClaimsPrincipal claimsPrincipal =
            RuntimeHostClaimsPrincipalFactory.Create(
                CreatePrincipal());

        Assert.Equal(
            "client-01",
            claimsPrincipal.FindFirstValue(
                RuntimeHostClientClaimTypes.PrincipalId));
    }

    [Fact]
    public void Create_ShouldProjectCredentialId()
    {
        ClaimsPrincipal claimsPrincipal =
            RuntimeHostClaimsPrincipalFactory.Create(
                CreatePrincipal());

        Assert.Equal(
            "certificate-01",
            claimsPrincipal.FindFirstValue(
                RuntimeHostClientClaimTypes.CredentialId));
    }

    [Fact]
    public void Create_ShouldProjectAuthenticationMechanism()
    {
        ClaimsPrincipal claimsPrincipal =
            RuntimeHostClaimsPrincipalFactory.Create(
                CreatePrincipal());

        Assert.Equal(
            "mutual-tls",
            claimsPrincipal.FindFirstValue(
                RuntimeHostClientClaimTypes.AuthenticationMechanism));
    }

    [Fact]
    public void Create_ShouldProjectRoundTripUtcAuthenticationTime()
    {
        ClaimsPrincipal claimsPrincipal =
            RuntimeHostClaimsPrincipalFactory.Create(
                CreatePrincipal());

        string value =
            Assert.IsType<string>(
                claimsPrincipal.FindFirstValue(
                    RuntimeHostClientClaimTypes.AuthenticatedAtUtc));

        Assert.Equal(
            AuthenticationTimeUtc,
            DateTimeOffset.Parse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind));
    }

    [Fact]
    public void Create_ShouldProjectTrustPolicyId()
    {
        ClaimsPrincipal claimsPrincipal =
            RuntimeHostClaimsPrincipalFactory.Create(
                CreatePrincipal());

        Assert.Equal(
            "trust-v1",
            claimsPrincipal.FindFirstValue(
                RuntimeHostClientClaimTypes.TrustPolicyId));
    }

    [Fact]
    public void Create_ShouldCreateExactlyOneIdentityAndFiveClaims()
    {
        ClaimsPrincipal claimsPrincipal =
            RuntimeHostClaimsPrincipalFactory.Create(
                CreatePrincipal());

        ClaimsIdentity identity =
            Assert.Single(
                claimsPrincipal.Identities);

        Assert.Equal(
            5,
            identity.Claims.Count());
    }

    [Fact]
    public void Create_MissingPrincipal_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(
            () => RuntimeHostClaimsPrincipalFactory.Create(
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
