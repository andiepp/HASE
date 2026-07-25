namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostClientPrincipalTests
{
    [Fact]
    public void Constructor_ValidValues_ShouldPreserveValues()
    {
        DateTimeOffset authenticatedAtUtc =
            new(
                2026,
                7,
                25,
                19,
                30,
                0,
                TimeSpan.Zero);

        RuntimeHostClientPrincipal principal =
            new(
                "protocol-explorer",
                "certificate-01",
                "mutual-tls",
                authenticatedAtUtc,
                "development-trust-v1");

        Assert.Equal(
            "protocol-explorer",
            principal.PrincipalId);
        Assert.Equal(
            "certificate-01",
            principal.CredentialId);
        Assert.Equal(
            "mutual-tls",
            principal.AuthenticationMechanism);
        Assert.Equal(
            authenticatedAtUtc,
            principal.AuthenticatedAtUtc);
        Assert.Equal(
            "development-trust-v1",
            principal.TrustPolicyId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_InvalidPrincipalId_ShouldThrow(
        string? principalId)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                new RuntimeHostClientPrincipal(
                    principalId!,
                    "credential-01",
                    "mutual-tls",
                    DateTimeOffset.UtcNow,
                    "trust-v1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_InvalidCredentialId_ShouldThrow(
        string? credentialId)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                new RuntimeHostClientPrincipal(
                    "client-01",
                    credentialId!,
                    "mutual-tls",
                    DateTimeOffset.UtcNow,
                    "trust-v1"));
    }

    [Fact]
    public void Constructor_NonUtcTimestamp_ShouldThrow()
    {
        DateTimeOffset nonUtcTimestamp =
            new(
                2026,
                7,
                25,
                21,
                30,
                0,
                TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            "authenticatedAtUtc",
            () =>
                new RuntimeHostClientPrincipal(
                    "client-01",
                    "credential-01",
                    "mutual-tls",
                    nonUtcTimestamp,
                    "trust-v1"));
    }
}
