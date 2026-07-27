using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemoteEndpointAttachmentGenerationTests
{
    [Fact]
    public void Constructor_Value_ShouldPreserveOpaqueGeneration()
    {
        Guid value =
            Guid.Parse(
                "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8");

        var generation =
            new RemoteEndpointAttachmentGeneration(
                value);

        Assert.Equal(
            value,
            generation.Value);
        Assert.Equal(
            "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8",
            generation.ToString());
    }

    [Fact]
    public void Constructor_EmptyValue_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "value",
            () => new RemoteEndpointAttachmentGeneration(
                Guid.Empty));
    }
}
