namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostRemoteOperationTests
{
    [Fact]
    public void Values_ShouldRemainStable()
    {
        Assert.Equal(
            0,
            (int)RuntimeHostRemoteOperation.Unspecified);
        Assert.Equal(
            1,
            (int)RuntimeHostRemoteOperation.GetSnapshot);
        Assert.Equal(
            2,
            (int)RuntimeHostRemoteOperation.ReadCachedProperty);
        Assert.Equal(
            3,
            (int)RuntimeHostRemoteOperation.ReadAuthoritativeProperty);
        Assert.Equal(
            4,
            (int)RuntimeHostRemoteOperation.WriteProperty);
        Assert.Equal(
            5,
            (int)RuntimeHostRemoteOperation.ExecuteCommand);
        Assert.Equal(
            6,
            (int)RuntimeHostRemoteOperation.Observe);
    }

    [Fact]
    public void Values_ShouldBeContiguous()
    {
        RuntimeHostRemoteOperation[] values =
            Enum.GetValues<RuntimeHostRemoteOperation>();

        Assert.Equal(
            [
                RuntimeHostRemoteOperation.Unspecified,
                RuntimeHostRemoteOperation.GetSnapshot,
                RuntimeHostRemoteOperation.ReadCachedProperty,
                RuntimeHostRemoteOperation.ReadAuthoritativeProperty,
                RuntimeHostRemoteOperation.WriteProperty,
                RuntimeHostRemoteOperation.ExecuteCommand,
                RuntimeHostRemoteOperation.Observe
            ],
            values);
    }
}
