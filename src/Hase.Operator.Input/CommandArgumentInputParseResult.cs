namespace Hase.Operator.Input;

/// <summary>
/// Represents the non-throwing result of converting operator input into one
/// normalized typed Command argument.
/// </summary>
public sealed record CommandArgumentInputParseResult
{
    private CommandArgumentInputParseResult(
        bool isSuccess,
        bool hasArgument,
        object? value,
        CommandArgumentInputFailure failure,
        string message)
    {
        IsSuccess =
            isSuccess;
        HasArgument =
            hasArgument;
        Value =
            value;
        Failure =
            failure;
        Message =
            message;
    }

    public bool IsSuccess
    {
        get;
    }

    public bool HasArgument
    {
        get;
    }

    public object? Value
    {
        get;
    }

    public CommandArgumentInputFailure Failure
    {
        get;
    }

    public string Message
    {
        get;
    }

    public static CommandArgumentInputParseResult Parameterless()
    {
        return new CommandArgumentInputParseResult(
            true,
            false,
            null,
            CommandArgumentInputFailure.None,
            string.Empty);
    }

    public static CommandArgumentInputParseResult Success(
        object value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        return new CommandArgumentInputParseResult(
            true,
            true,
            value,
            CommandArgumentInputFailure.None,
            string.Empty);
    }

    public static CommandArgumentInputParseResult Failed(
        CommandArgumentInputFailure failure,
        string message)
    {
        if (failure == CommandArgumentInputFailure.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failure),
                "A failed result requires a failure kind.");
        }

        if (string.IsNullOrWhiteSpace(
                message))
        {
            throw new ArgumentException(
                "A failed result requires a message.",
                nameof(message));
        }

        return new CommandArgumentInputParseResult(
            false,
            false,
            null,
            failure,
            message.Trim());
    }
}
