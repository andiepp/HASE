namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostAuthenticationValueTests
{
    [Fact]
    public void PrincipalId_EqualValues_ShouldBeEqual()
    {
        RuntimeHostClientPrincipalId first =
            new(
                "protocol-explorer");
        RuntimeHostClientPrincipalId second =
            new(
                "protocol-explorer");

        Assert.Equal(
            first,
            second);
        Assert.Equal(
            "protocol-explorer",
            first.ToString());
    }

    [Fact]
    public void CredentialId_DifferentValues_ShouldNotBeEqual()
    {
        RuntimeHostClientCredentialId first =
            new(
                "certificate-01");
        RuntimeHostClientCredentialId second =
            new(
                "certificate-02");

        Assert.NotEqual(
            first,
            second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void PrincipalId_InvalidValue_ShouldThrow(
        string? value)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                new RuntimeHostClientPrincipalId(
                    value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CredentialId_InvalidValue_ShouldThrow(
        string? value)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                new RuntimeHostClientCredentialId(
                    value!));
    }

    [Fact]
    public void MutualTls_ShouldUseStableValue()
    {
        Assert.Equal(
            "mutual-tls",
            RuntimeHostAuthenticationMechanism.MutualTls.Value);
    }

    [Fact]
    public void TrustedLoopback_ShouldUseStableValue()
    {
        Assert.Equal(
            "trusted-loopback",
            RuntimeHostAuthenticationMechanism.TrustedLoopback.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AuthenticationMechanism_InvalidValue_ShouldThrow(
        string? value)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                new RuntimeHostAuthenticationMechanism(
                    value!));
    }
}
