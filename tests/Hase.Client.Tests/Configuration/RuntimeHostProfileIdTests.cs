using Hase.Client.Configuration;

namespace Hase.Client.Tests.Configuration;

public sealed class RuntimeHostProfileIdTests
{
    [Theory]
    [InlineData("laboratory-desktop")]
    [InlineData("vacation-site")]
    [InlineData("bench.2")]
    [InlineData("host_01")]
    [InlineData("0")]
    public void Constructor_ValidValue_ShouldPreserveIdentity(
        string value)
    {
        var profileId =
            new RuntimeHostProfileId(
                value);

        Assert.Equal(
            value,
            profileId.Value);
        Assert.Equal(
            value,
            profileId.ToString());
    }

    [Fact]
    public void Constructor_SurroundingWhitespace_ShouldTrim()
    {
        var profileId =
            new RuntimeHostProfileId(
                "  laboratory-desktop  ");

        Assert.Equal(
            "laboratory-desktop",
            profileId.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Laboratory")]
    [InlineData("-laboratory")]
    [InlineData("_laboratory")]
    [InlineData(".laboratory")]
    [InlineData("laboratory desktop")]
    [InlineData("laboratory/desktop")]
    [InlineData("laboratory:desktop")]
    public void Constructor_InvalidValue_ShouldThrow(
        string? value)
    {
        Assert.Throws<ArgumentException>(
            "value",
            () => new RuntimeHostProfileId(
                value!));
    }

    [Fact]
    public void Constructor_OverMaximumLength_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "value",
            () => new RuntimeHostProfileId(
                new string(
                    'a',
                    RuntimeHostProfileId.MaximumLength + 1)));
    }

    [Fact]
    public void Equality_SameValue_ShouldBeOrdinal()
    {
        Assert.Equal(
            new RuntimeHostProfileId(
                "host-01"),
            new RuntimeHostProfileId(
                "host-01"));
    }
}
