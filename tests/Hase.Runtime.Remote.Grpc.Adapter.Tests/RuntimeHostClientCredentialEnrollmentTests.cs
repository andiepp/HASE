namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostClientCredentialEnrollmentTests
{
    [Fact]
    public void Constructor_ValidValues_ShouldPreserveValues()
    {
        RuntimeHostClientCredentialIdentity credentialIdentity =
            CreateCredentialIdentity();
        RuntimeHostClientPrincipalId principalId =
            new(
                "protocol-explorer");

        RuntimeHostClientCredentialEnrollment enrollment =
            new(
                credentialIdentity,
                principalId,
                "development-trust-v1");

        Assert.Equal(
            credentialIdentity,
            enrollment.CredentialIdentity);
        Assert.Equal(
            principalId,
            enrollment.PrincipalId);
        Assert.Equal(
            "development-trust-v1",
            enrollment.TrustPolicyId);
    }

    [Fact]
    public void CreatePrincipal_ShouldPreserveEnrollmentAndAuthenticationTime()
    {
        DateTimeOffset authenticatedAtUtc =
            new(
                2026,
                7,
                25,
                20,
                30,
                0,
                TimeSpan.Zero);
        RuntimeHostClientCredentialEnrollment enrollment =
            new(
                CreateCredentialIdentity(),
                new RuntimeHostClientPrincipalId(
                    "protocol-explorer"),
                "development-trust-v1");

        RuntimeHostClientPrincipal principal =
            enrollment.CreatePrincipal(
                authenticatedAtUtc);

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
    public void Constructor_DefaultCredentialIdentity_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "credentialIdentity",
            () =>
                new RuntimeHostClientCredentialEnrollment(
                    default,
                    new RuntimeHostClientPrincipalId(
                        "client-01"),
                    "trust-v1"));
    }

    [Fact]
    public void Constructor_DefaultPrincipalId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "principalId",
            () =>
                new RuntimeHostClientCredentialEnrollment(
                    CreateCredentialIdentity(),
                    default,
                    "trust-v1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_InvalidTrustPolicyId_ShouldThrow(
        string? trustPolicyId)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                new RuntimeHostClientCredentialEnrollment(
                    CreateCredentialIdentity(),
                    new RuntimeHostClientPrincipalId(
                        "client-01"),
                    trustPolicyId!));
    }

    private static RuntimeHostClientCredentialIdentity CreateCredentialIdentity()
    {
        return new RuntimeHostClientCredentialIdentity(
            RuntimeHostAuthenticationMechanism.MutualTls,
            new RuntimeHostClientCredentialId(
                "certificate-01"));
    }
}
