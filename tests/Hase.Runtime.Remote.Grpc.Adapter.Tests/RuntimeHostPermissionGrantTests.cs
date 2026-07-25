namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostPermissionGrantTests
{
    [Fact]
    public void Constructor_ValidValues_ShouldPreserveValues()
    {
        RuntimeHostPermissionGrant grant =
            new(
                "client-01",
                RuntimeHostPermission.ExecuteCommand);

        Assert.Equal(
            "client-01",
            grant.PrincipalId);
        Assert.Equal(
            RuntimeHostPermission.ExecuteCommand,
            grant.Permission);
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
                new RuntimeHostPermissionGrant(
                    principalId!,
                    RuntimeHostPermission.ReadSnapshot));
    }

    [Fact]
    public void Constructor_DefaultPermission_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "permission",
            () =>
                new RuntimeHostPermissionGrant(
                    "client-01",
                    default));
    }
}
