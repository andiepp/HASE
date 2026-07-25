namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostClientPrincipalTests
{
    [Fact]
    public void Constructor_ValidStringValues_ShouldPreserveValues()
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

    [Fact]
    public void Constructor_ValidTypedValues_ShouldPreserveBothViews()
    {
        RuntimeHostClientPrincipalId principalId =
            new(
                "remote-application-01");
        RuntimeHostClientCredentialId credentialId =
            new(
                "client-certificate-01");
        DateTimeOffset authenticatedAtUtc =
            new(
                2026,
                7,
                25,
                20,
                0,
                0,
                TimeSpan.Zero);

        RuntimeHostClientPrincipal principal =
            new(
                principalId,
                credentialId,
                RuntimeHostAuthenticationMechanism.MutualTls,
                authenticatedAtUtc,
                "development-trust-v1");

        Assert.Equal(
            principalId,
            principal.PrincipalIdentifier);
        Assert.Equal(
            credentialId,
            principal.CredentialIdentifier);
        Assert.Equal(
            RuntimeHostAuthenticationMechanism.MutualTls,
            principal.AuthenticationMechanismValue);
        Assert.Equal(
            principalId.Value,
            principal.PrincipalId);
        Assert.Equal(
            credentialId.Value,
            principal.CredentialId);
        Assert.Equal(
            RuntimeHostAuthenticationMechanism.MutualTls.Value,
            principal.AuthenticationMechanism);
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
    public void Constructor_DefaultTypedPrincipalId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "principalIdentifier",
            () =>
                new RuntimeHostClientPrincipal(
                    default,
                    new RuntimeHostClientCredentialId(
                        "credential-01"),
                    RuntimeHostAuthenticationMechanism.MutualTls,
                    DateTimeOffset.UtcNow,
                    "trust-v1"));
    }

    [Fact]
    public void Constructor_DefaultTypedCredentialId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "credentialIdentifier",
            () =>
                new RuntimeHostClientPrincipal(
                    new RuntimeHostClientPrincipalId(
                        "client-01"),
                    default,
                    RuntimeHostAuthenticationMechanism.MutualTls,
                    DateTimeOffset.UtcNow,
                    "trust-v1"));
    }

    [Fact]
    public void Constructor_DefaultAuthenticationMechanism_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "authenticationMechanismValue",
            () =>
                new RuntimeHostClientPrincipal(
                    new RuntimeHostClientPrincipalId(
                        "client-01"),
                    new RuntimeHostClientCredentialId(
                        "credential-01"),
                    default,
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
