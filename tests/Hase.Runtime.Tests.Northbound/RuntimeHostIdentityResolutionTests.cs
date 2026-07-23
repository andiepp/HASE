using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostIdentityResolutionTests
{
    [Theory]
    [InlineData(RuntimeHostIdentityOrigin.ExplicitConfiguration)]
    [InlineData(RuntimeHostIdentityOrigin.Persisted)]
    [InlineData(RuntimeHostIdentityOrigin.GeneratedAndPersisted)]
    public void Constructor_StoresIdentityAndDefinedOrigin(
        RuntimeHostIdentityOrigin origin)
    {
        var runtimeHostId =
            new RuntimeHostId(
                "workshop-runtime");

        var resolution =
            new RuntimeHostIdentityResolution(
                runtimeHostId,
                origin);

        Assert.Same(
            runtimeHostId,
            resolution.RuntimeHostId);

        Assert.Equal(
            origin,
            resolution.Origin);
    }

    [Fact]
    public void Constructor_NullIdentity_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostIdentityResolution(
                null!,
                RuntimeHostIdentityOrigin.Persisted));
    }

    [Fact]
    public void Constructor_UndefinedOrigin_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RuntimeHostIdentityResolution(
                new RuntimeHostId(
                    "workshop-runtime"),
                (RuntimeHostIdentityOrigin)999));
    }
}