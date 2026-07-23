using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostIdTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_MissingValue_Throws(
        string? value)
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeHostId(
                value!));
    }

    [Fact]
    public void Constructor_TrimsValue()
    {
        var runtimeHostId =
            new RuntimeHostId(
                "  workshop-runtime  ");

        Assert.Equal(
            "workshop-runtime",
            runtimeHostId.Value);

        Assert.Equal(
            "workshop-runtime",
            runtimeHostId.ToString());
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        Assert.Equal(
            new RuntimeHostId(
                "runtime-host-58c50d84-c4ad-47a0-b7c6-1eeed3483593"),
            new RuntimeHostId(
                "runtime-host-58c50d84-c4ad-47a0-b7c6-1eeed3483593"));
    }

    [Fact]
    public void DifferentValues_AreNotEqual()
    {
        Assert.NotEqual(
            new RuntimeHostId(
                "first-runtime"),
            new RuntimeHostId(
                "second-runtime"));
    }
}