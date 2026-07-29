namespace Hase.Operator.Input.Tests;

public sealed class PropertyInputParseResultTests
{
    [Fact]
    public void Success_ShouldExposeValueWithoutFailure()
    {
        object value =
            new();

        PropertyInputParseResult result =
            PropertyInputParseResult.Success(
                value);

        Assert.True(
            result.IsSuccess);
        Assert.Same(
            value,
            result.Value);
        Assert.Equal(
            PropertyInputFailure.None,
            result.Failure);
        Assert.Empty(
            result.Message);
    }

    [Fact]
    public void Failed_ShouldExposeFailureWithoutValue()
    {
        PropertyInputParseResult result =
            PropertyInputParseResult.Failed(
                PropertyInputFailure.InvalidFormat,
                "Invalid input.");

        Assert.False(
            result.IsSuccess);
        Assert.Null(
            result.Value);
        Assert.Equal(
            PropertyInputFailure.InvalidFormat,
            result.Failure);
        Assert.Equal(
            "Invalid input.",
            result.Message);
    }

    [Fact]
    public void Failed_WithNone_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                PropertyInputParseResult.Failed(
                    PropertyInputFailure.None,
                    "Invalid input."));
    }
}
