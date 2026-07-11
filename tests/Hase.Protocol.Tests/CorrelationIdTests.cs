using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class CorrelationIdTests
{
    [Fact]
    public void Constructor_SetsValue()
    {
        CorrelationId id = new(123);

        Assert.Equal((uint)123, id.Value);
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        Assert.Equal(
            new CorrelationId(42),
            new CorrelationId(42));
    }

    [Fact]
    public void DifferentValues_AreNotEqual()
    {
        Assert.NotEqual(
            new CorrelationId(1),
            new CorrelationId(2));
    }

    [Fact]
    public void None_HasValueZero()
    {
        Assert.Equal(
            new CorrelationId(0),
            CorrelationId.None);
    }

    [Fact]
    public void IsNone_ReturnsTrue_ForZero()
    {
        Assert.True(CorrelationId.None.IsNone);
    }

    [Fact]
    public void IsNone_ReturnsFalse_ForNonZero()
    {
        Assert.False(new CorrelationId(17).IsNone);
    }

    [Fact]
    public void ToString_ReturnsNumericValue()
    {
        Assert.Equal(
            "4711",
            new CorrelationId(4711).ToString());
    }
}