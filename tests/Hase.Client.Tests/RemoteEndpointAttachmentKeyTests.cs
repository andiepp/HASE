using Hase.Client;
using Hase.Core.Domain.Identity;

namespace Hase.Client.Tests;

public sealed class RemoteEndpointAttachmentKeyTests
{
    [Fact]
    public void Constructor_Values_ShouldPreserveIdentityAndGeneration()
    {
        var endpointId =
            new EndpointId(
                "endpoint-01");
        var generation =
            CreateGeneration();

        var key =
            new RemoteEndpointAttachmentKey(
                endpointId,
                generation);

        Assert.Same(
            endpointId,
            key.EndpointId);
        Assert.Same(
            generation,
            key.Generation);
        Assert.Equal(
            $"endpoint-01@{generation}",
            key.ToString());
    }

    [Fact]
    public void Constructor_NullEndpointId_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "endpointId",
            () => new RemoteEndpointAttachmentKey(
                null!,
                CreateGeneration()));
    }

    [Fact]
    public void Constructor_NullGeneration_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "generation",
            () => new RemoteEndpointAttachmentKey(
                new EndpointId(
                    "endpoint-01"),
                null!));
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        Guid generationValue =
            Guid.Parse(
                "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8");

        var first =
            new RemoteEndpointAttachmentKey(
                new EndpointId(
                    "endpoint-01"),
                new RemoteEndpointAttachmentGeneration(
                    generationValue));
        var second =
            new RemoteEndpointAttachmentKey(
                new EndpointId(
                    "endpoint-01"),
                new RemoteEndpointAttachmentGeneration(
                    generationValue));

        Assert.Equal(
            first,
            second);
    }

    private static RemoteEndpointAttachmentGeneration CreateGeneration()
    {
        return new RemoteEndpointAttachmentGeneration(
            Guid.Parse(
                "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8"));
    }
}
