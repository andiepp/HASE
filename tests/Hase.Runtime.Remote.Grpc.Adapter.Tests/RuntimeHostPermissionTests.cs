namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostPermissionTests
{
    [Fact]
    public void VersionOnePermissions_ShouldHaveStableValues()
    {
        Assert.Equal(
            "runtime-host.snapshot.read",
            RuntimeHostPermission.ReadSnapshot.Value);
        Assert.Equal(
            "property.cached.read",
            RuntimeHostPermission.ReadCachedProperty.Value);
        Assert.Equal(
            "property.authoritative.read",
            RuntimeHostPermission.ReadAuthoritativeProperty.Value);
        Assert.Equal(
            "property.write",
            RuntimeHostPermission.WriteProperty.Value);
        Assert.Equal(
            "command.execute",
            RuntimeHostPermission.ExecuteCommand.Value);
        Assert.Equal(
            "observation.subscribe",
            RuntimeHostPermission.SubscribeObservation.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_InvalidValue_ShouldThrow(
        string? value)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                new RuntimeHostPermission(
                    value!));
    }

    [Fact]
    public void ToString_ShouldReturnStableValue()
    {
        RuntimeHostPermission permission =
            new("custom.permission");

        Assert.Equal(
            "custom.permission",
            permission.ToString());
    }
}
