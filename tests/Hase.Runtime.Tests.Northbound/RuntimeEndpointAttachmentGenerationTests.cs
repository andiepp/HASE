using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeEndpointAttachmentGenerationTests
{
    [Fact]
    public void Constructor_EmptyValue_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeEndpointAttachmentGeneration(
                Guid.Empty));
    }

    [Fact]
    public void Constructor_NonEmptyValue_StoresValue()
    {
        var value =
            Guid.Parse(
                "cbd59cfe-f140-4eeb-a3b4-69c7df52b487");

        var generation =
            new RuntimeEndpointAttachmentGeneration(
                value);

        Assert.Equal(
            value,
            generation.Value);
    }

    [Fact]
    public void CreateNew_ReturnsNonEmptyValue()
    {
        RuntimeEndpointAttachmentGeneration generation =
            RuntimeEndpointAttachmentGeneration.CreateNew();

        Assert.NotEqual(
            Guid.Empty,
            generation.Value);
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        var value =
            Guid.Parse(
                "f5713fac-5c17-4a29-ae5d-c87bb5407d07");

        Assert.Equal(
            new RuntimeEndpointAttachmentGeneration(
                value),
            new RuntimeEndpointAttachmentGeneration(
                value));
    }

    [Fact]
    public void ToString_ReturnsCanonicalGuid()
    {
        var generation =
            new RuntimeEndpointAttachmentGeneration(
                Guid.Parse(
                    "75fb4422-ec37-44e3-8920-0013c8be6488"));

        Assert.Equal(
            "75fb4422-ec37-44e3-8920-0013c8be6488",
            generation.ToString());
    }
}