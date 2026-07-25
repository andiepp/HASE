namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostAuthenticationResultTests
{
    [Fact]
    public void Authenticated_ShouldPreservePrincipal()
    {
        RuntimeHostClientPrincipal principal =
            CreatePrincipal();

        RuntimeHostAuthenticationResult result =
            RuntimeHostAuthenticationResult.Authenticated(
                principal);

        Assert.True(
            result.IsAuthenticated);
        Assert.Same(
            principal,
            result.Principal);
        Assert.Equal(
            RuntimeHostAuthenticationFailureReason.None,
            result.FailureReason);
    }

    [Fact]
    public void Authenticated_NullPrincipal_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "principal",
            () =>
                RuntimeHostAuthenticationResult.Authenticated(
                    null!));
    }

    [Fact]
    public void Failed_ShouldPreserveReason()
    {
        RuntimeHostAuthenticationResult result =
            RuntimeHostAuthenticationResult.Failed(
                RuntimeHostAuthenticationFailureReason.UnknownCredential);

        Assert.False(
            result.IsAuthenticated);
        Assert.Null(
            result.Principal);
        Assert.Equal(
            RuntimeHostAuthenticationFailureReason.UnknownCredential,
            result.FailureReason);
    }

    [Fact]
    public void Failed_NoneReason_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "failureReason",
            () =>
                RuntimeHostAuthenticationResult.Failed(
                    RuntimeHostAuthenticationFailureReason.None));
    }

    [Fact]
    public void Failed_UnknownEnumValue_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "failureReason",
            () =>
                RuntimeHostAuthenticationResult.Failed(
                    (RuntimeHostAuthenticationFailureReason)999));
    }

    private static RuntimeHostClientPrincipal CreatePrincipal()
    {
        return new RuntimeHostClientPrincipal(
            new RuntimeHostClientPrincipalId(
                "client-01"),
            new RuntimeHostClientCredentialId(
                "certificate-01"),
            RuntimeHostAuthenticationMechanism.MutualTls,
            new DateTimeOffset(
                2026,
                7,
                25,
                21,
                30,
                0,
                TimeSpan.Zero),
            "development-trust-v1");
    }
}
