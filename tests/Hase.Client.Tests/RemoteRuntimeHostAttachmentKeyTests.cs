using Hase.Client;
using Hase.Core.Domain.Identity;

namespace Hase.Client.Tests;

public sealed class RemoteRuntimeHostAttachmentKeyTests
{
    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        Guid generation = Guid.Parse("0a11d9d4-7a02-43be-ae3f-eef9d11e0de8");

        var first = Create("host-01", "endpoint-01", generation);
        var second = Create("host-01", "endpoint-01", generation);

        Assert.Equal(first, second);
        Assert.Equal($"host-01/endpoint-01@{generation:D}", first.ToString());
    }

    [Fact]
    public void Equality_SameAttachmentOnDifferentHosts_ShouldRemainDistinct()
    {
        Guid generation = Guid.Parse("0a11d9d4-7a02-43be-ae3f-eef9d11e0de8");

        Assert.NotEqual(
            Create("host-01", "endpoint-01", generation),
            Create("host-02", "endpoint-01", generation));
    }

    [Fact]
    public void Equality_DifferentGenerations_ShouldRemainDistinct()
    {
        Assert.NotEqual(
            Create("host-01", "endpoint-01", Guid.NewGuid()),
            Create("host-01", "endpoint-01", Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_NullValues_ShouldThrow()
    {
        RemoteEndpointAttachmentKey attachment = Create("host-01", "endpoint-01", Guid.NewGuid()).Attachment;

        Assert.Throws<ArgumentNullException>(
            "runtimeHostId",
            () => new RemoteRuntimeHostAttachmentKey(null!, attachment));
        Assert.Throws<ArgumentNullException>(
            "attachment",
            () => new RemoteRuntimeHostAttachmentKey(new RemoteRuntimeHostId("host-01"), null!));
    }

    private static RemoteRuntimeHostAttachmentKey Create(
        string hostId,
        string endpointId,
        Guid generation) =>
        new(
            new RemoteRuntimeHostId(hostId),
            new RemoteEndpointAttachmentKey(
                new EndpointId(endpointId),
                new RemoteEndpointAttachmentGeneration(generation)));
}
