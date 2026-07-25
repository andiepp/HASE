namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class TrustedLoopbackRuntimeHostClientPrincipalProviderTests
{
    [Fact]
    public void Constructor_NullPrincipal_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "principal",
            () =>
                new TrustedLoopbackRuntimeHostClientPrincipalProvider(
                    null!));
    }

    [Fact]
    public void GetPrincipal_ShouldReturnConfiguredPrincipal()
    {
        RuntimeHostClientPrincipal principal =
            CreatePrincipal();

        TrustedLoopbackRuntimeHostClientPrincipalProvider provider =
            new(
                principal);

        Assert.Same(
            principal,
            provider.GetPrincipal(
                null));
    }

    private static RuntimeHostClientPrincipal CreatePrincipal()
    {
        return new RuntimeHostClientPrincipal(
            "trusted-loopback-client",
            "trusted-loopback-profile",
            "trusted-loopback",
            new DateTimeOffset(
                2026,
                7,
                25,
                21,
                0,
                0,
                TimeSpan.Zero),
            "trusted-loopback-v1");
    }
}
