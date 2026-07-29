namespace Hase.Operator.Input.Tests;

public sealed class CommandArgumentInputParseResultTests
{
    [Fact]
    public void Parameterless_ShouldExposeSuccessfulNoArgumentResult()
    {
        CommandArgumentInputParseResult result =
            CommandArgumentInputParseResult.Parameterless();

        Assert.True(
            result.IsSuccess);
        Assert.False(
            result.HasArgument);
        Assert.Null(
            result.Value);
        Assert.Equal(
            CommandArgumentInputFailure.None,
            result.Failure);
        Assert.Empty(
            result.Message);
    }

    [Fact]
    public void Success_ShouldExposeTypedArgument()
    {
        object value =
            new();

        CommandArgumentInputParseResult result =
            CommandArgumentInputParseResult.Success(
                value);

        Assert.True(
            result.IsSuccess);
        Assert.True(
            result.HasArgument);
        Assert.Same(
            value,
            result.Value);
        Assert.Equal(
            CommandArgumentInputFailure.None,
            result.Failure);
        Assert.Empty(
            result.Message);
    }

    [Fact]
    public void Failed_ShouldExposeFailureWithoutArgument()
    {
        CommandArgumentInputParseResult result =
            CommandArgumentInputParseResult.Failed(
                CommandArgumentInputFailure.InvalidFormat,
                "Invalid input.");

        Assert.False(
            result.IsSuccess);
        Assert.False(
            result.HasArgument);
        Assert.Null(
            result.Value);
        Assert.Equal(
            CommandArgumentInputFailure.InvalidFormat,
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
                CommandArgumentInputParseResult.Failed(
                    CommandArgumentInputFailure.None,
                    "Invalid input."));
    }
}
