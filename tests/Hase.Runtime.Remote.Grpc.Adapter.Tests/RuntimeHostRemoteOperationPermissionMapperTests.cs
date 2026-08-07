namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostRemoteOperationPermissionMapperTests
{
    [Theory]
    [MemberData(nameof(Mappings))]
    public void Map_SpecifiedOperation_ShouldReturnRequiredPermission(
        RuntimeHostRemoteOperation operation,
        RuntimeHostPermission expectedPermission)
    {
        RuntimeHostRemoteOperationPermissionMapper mapper =
            new();

        RuntimeHostPermission permission =
            mapper.Map(operation);

        Assert.Equal(
            expectedPermission,
            permission);
    }

    [Fact]
    public void Map_UnspecifiedOperation_ShouldThrow()
    {
        RuntimeHostRemoteOperationPermissionMapper mapper =
            new();

        Assert.Throws<ArgumentOutOfRangeException>(
            "operation",
            () =>
                mapper.Map(
                    RuntimeHostRemoteOperation.Unspecified));
    }

    [Fact]
    public void Map_UnknownOperation_ShouldThrow()
    {
        RuntimeHostRemoteOperationPermissionMapper mapper =
            new();

        Assert.Throws<ArgumentOutOfRangeException>(
            "operation",
            () =>
                mapper.Map(
                    (RuntimeHostRemoteOperation)int.MaxValue));
    }

    [Fact]
    public void Map_EverySpecifiedOperation_ShouldBeCovered()
    {
        RuntimeHostRemoteOperationPermissionMapper mapper =
            new();

        RuntimeHostRemoteOperation[] operations =
            Enum.GetValues<RuntimeHostRemoteOperation>()
                .Where(
                    operation =>
                        operation
                        != RuntimeHostRemoteOperation.Unspecified)
                .ToArray();

        RuntimeHostPermission[] permissions =
            operations
                .Select(mapper.Map)
                .ToArray();

        Assert.Equal(
            operations.Length,
            permissions.Length);
        Assert.All(
            permissions,
            permission =>
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        permission.Value)));
    }

    public static TheoryData<
        RuntimeHostRemoteOperation,
        RuntimeHostPermission> Mappings =>
        new()
        {
            {
                RuntimeHostRemoteOperation.GetSnapshot,
                RuntimeHostPermission.ReadSnapshot
            },
            {
                RuntimeHostRemoteOperation.ReadCachedProperty,
                RuntimeHostPermission.ReadCachedProperty
            },
            {
                RuntimeHostRemoteOperation.ReadAuthoritativeProperty,
                RuntimeHostPermission.ReadAuthoritativeProperty
            },
            {
                RuntimeHostRemoteOperation.WriteProperty,
                RuntimeHostPermission.WriteProperty
            },
            {
                RuntimeHostRemoteOperation.ExecuteCommand,
                RuntimeHostPermission.ExecuteCommand
            },
            {
                RuntimeHostRemoteOperation.Observe,
                RuntimeHostPermission.SubscribeObservation
            },
            {
                RuntimeHostRemoteOperation.ObserveDiagnostics,
                RuntimeHostPermission.SubscribeDiagnostics
            }
        };
}
