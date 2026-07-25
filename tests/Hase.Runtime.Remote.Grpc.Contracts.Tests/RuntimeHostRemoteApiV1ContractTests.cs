using Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Contracts.Tests;

public sealed class RuntimeHostRemoteApiV1ContractTests
{
    [Fact]
    public void Contract_UsesVersionedPackage()
    {
        Assert.Equal(
            "hase.runtime.remote.v1",
            RuntimeHostRemoteApiV1Reflection.Descriptor.Package);
    }

    [Fact]
    public void Contract_DefinesUnarySnapshotOperation()
    {
        var service = Assert.Single(
            RuntimeHostRemoteApiV1Reflection.Descriptor.Services);

        Assert.Equal("RuntimeHostRemoteApi", service.Name);

        var method = Assert.Single(service.Methods);

        Assert.Equal("GetSnapshot", method.Name);
        Assert.False(method.IsClientStreaming);
        Assert.False(method.IsServerStreaming);
        Assert.Equal(
            GetSnapshotRequest.Descriptor.FullName,
            method.InputType.FullName);
        Assert.Equal(
            GetSnapshotResponse.Descriptor.FullName,
            method.OutputType.FullName);
    }

    [Fact]
    public void SnapshotMessages_StartWithoutPrematureFields()
    {
        Assert.Empty(GetSnapshotRequest.Descriptor.Fields.InDeclarationOrder());
        Assert.Empty(GetSnapshotResponse.Descriptor.Fields.InDeclarationOrder());
    }
}