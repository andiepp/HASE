namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostClientCredentialIdentityTests
{
    [Fact]
    public void Constructor_ValidValues_ShouldPreserveValues()
    {
        RuntimeHostClientCredentialIdentity identity =
            new(
                RuntimeHostAuthenticationMechanism.MutualTls,
                new RuntimeHostClientCredentialId(
                    "certificate-01"));

        Assert.Equal(
            RuntimeHostAuthenticationMechanism.MutualTls,
            identity.AuthenticationMechanism);
        Assert.Equal(
            new RuntimeHostClientCredentialId(
                "certificate-01"),
            identity.CredentialId);
        Assert.Equal(
            "mutual-tls:certificate-01",
            identity.ToString());
    }

    [Fact]
    public void EqualValues_ShouldBeEqual()
    {
        RuntimeHostClientCredentialIdentity first =
            new(
                RuntimeHostAuthenticationMechanism.MutualTls,
                new RuntimeHostClientCredentialId(
                    "certificate-01"));
        RuntimeHostClientCredentialIdentity second =
            new(
                RuntimeHostAuthenticationMechanism.MutualTls,
                new RuntimeHostClientCredentialId(
                    "certificate-01"));

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void DifferentMechanisms_ShouldNotBeEqual()
    {
        RuntimeHostClientCredentialId credentialId =
            new(
                "credential-01");

        RuntimeHostClientCredentialIdentity first =
            new(
                RuntimeHostAuthenticationMechanism.MutualTls,
                credentialId);
        RuntimeHostClientCredentialIdentity second =
            new(
                RuntimeHostAuthenticationMechanism.TrustedLoopback,
                credentialId);

        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void Constructor_DefaultMechanism_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "authenticationMechanism",
            () =>
                new RuntimeHostClientCredentialIdentity(
                    default,
                    new RuntimeHostClientCredentialId(
                        "credential-01")));
    }

    [Fact]
    public void Constructor_DefaultCredentialId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "credentialId",
            () =>
                new RuntimeHostClientCredentialIdentity(
                    RuntimeHostAuthenticationMechanism.MutualTls,
                    default));
    }
}
