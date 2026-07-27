using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RuntimeHostClientSessionStatusTests
{
    private static readonly RemoteRuntimeHostId RuntimeHostId =
        new(
            "host-01");

    private static readonly RuntimeHostClientApiVersion ApiVersion =
        new(
            1,
            0);

    [Theory]
    [InlineData(RuntimeHostClientSessionState.Disconnected)]
    [InlineData(RuntimeHostClientSessionState.Connecting)]
    [InlineData(RuntimeHostClientSessionState.Reconnecting)]
    [InlineData(RuntimeHostClientSessionState.Disconnecting)]
    [InlineData(RuntimeHostClientSessionState.Faulted)]
    public void Constructor_StateWithoutRetainedBaseline_ShouldSucceed(
        RuntimeHostClientSessionState state)
    {
        var status =
            new RuntimeHostClientSessionStatus(
                state);

        Assert.Equal(
            state,
            status.State);
        Assert.Null(
            status.RuntimeHostId);
        Assert.Null(
            status.ApiVersion);
    }

    [Fact]
    public void Constructor_ConnectedWithAuthoritativeBaseline_ShouldSucceed()
    {
        var status =
            new RuntimeHostClientSessionStatus(
                RuntimeHostClientSessionState.Connected,
                RuntimeHostId,
                ApiVersion);

        Assert.Equal(
            RuntimeHostClientSessionState.Connected,
            status.State);
        Assert.Same(
            RuntimeHostId,
            status.RuntimeHostId);
        Assert.Equal(
            ApiVersion,
            status.ApiVersion);
    }

    [Theory]
    [InlineData(RuntimeHostClientSessionState.Reconnecting)]
    [InlineData(RuntimeHostClientSessionState.Disconnecting)]
    [InlineData(RuntimeHostClientSessionState.Faulted)]
    public void Constructor_TransitionalStateWithRetainedBaseline_ShouldSucceed(
        RuntimeHostClientSessionState state)
    {
        var status =
            new RuntimeHostClientSessionStatus(
                state,
                RuntimeHostId,
                ApiVersion);

        Assert.Equal(
            state,
            status.State);
        Assert.Same(
            RuntimeHostId,
            status.RuntimeHostId);
        Assert.Equal(
            ApiVersion,
            status.ApiVersion);
    }

    [Fact]
    public void Constructor_UnspecifiedState_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "state",
            () => new RuntimeHostClientSessionStatus(
                RuntimeHostClientSessionState.Unspecified));
    }

    [Fact]
    public void Constructor_UndefinedState_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "state",
            () => new RuntimeHostClientSessionStatus(
                (RuntimeHostClientSessionState) 99));
    }

    [Fact]
    public void Constructor_IdentityWithoutVersion_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeHostClientSessionStatus(
                RuntimeHostClientSessionState.Reconnecting,
                RuntimeHostId));
    }

    [Fact]
    public void Constructor_VersionWithoutIdentity_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeHostClientSessionStatus(
                RuntimeHostClientSessionState.Reconnecting,
                apiVersion:
                    ApiVersion));
    }

    [Fact]
    public void Constructor_DefaultVersion_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "apiVersion",
            () => new RuntimeHostClientSessionStatus(
                RuntimeHostClientSessionState.Reconnecting,
                RuntimeHostId,
                default(RuntimeHostClientApiVersion)));
    }

    [Fact]
    public void Constructor_ConnectedWithoutBaseline_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "runtimeHostId",
            () => new RuntimeHostClientSessionStatus(
                RuntimeHostClientSessionState.Connected));
    }

    [Theory]
    [InlineData(RuntimeHostClientSessionState.Disconnected)]
    [InlineData(RuntimeHostClientSessionState.Connecting)]
    public void Constructor_PreBaselineStateWithRetainedBaseline_ShouldThrow(
        RuntimeHostClientSessionState state)
    {
        Assert.Throws<ArgumentException>(
            "runtimeHostId",
            () => new RuntimeHostClientSessionStatus(
                state,
                RuntimeHostId,
                ApiVersion));
    }
}
