namespace Hase.Operator.Input;

/// <summary>
/// Represents the non-throwing result of converting operator input into one
/// normalized typed Property value.
/// </summary>
public sealed record PropertyInputParseResult
{
    private PropertyInputParseResult(
        bool isSuccess,
        object? value,
        PropertyInputFailure failure,
        string message)
    {
        IsSuccess =
            isSuccess;
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

    public object? Value
    {
        get;
    }

    public PropertyInputFailure Failure
    {
        get;
    }

    public string Message
    {
        get;
    }

    public static PropertyInputParseResult Success(
        object value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        return new PropertyInputParseResult(
            true,
            value,
            PropertyInputFailure.None,
            string.Empty);
    }

    public static PropertyInputParseResult Failed(
        PropertyInputFailure failure,
        string message)
    {
        if (failure == PropertyInputFailure.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failure),
                "A failed result requires a failure kind.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "A failed result requires a message.",
                nameof(message));
        }

        return new PropertyInputParseResult(
            false,
            null,
            failure,
            message.Trim());
    }
}
