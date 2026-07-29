namespace Hase.Operator.Input;

internal sealed record DescriptorInputParseResult
{
    private DescriptorInputParseResult(
        bool isSuccess,
        object? value,
        DescriptorInputFailure failure,
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

    public DescriptorInputFailure Failure
    {
        get;
    }

    public string Message
    {
        get;
    }

    public static DescriptorInputParseResult Success(
        object value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        return new DescriptorInputParseResult(
            true,
            value,
            DescriptorInputFailure.None,
            string.Empty);
    }

    public static DescriptorInputParseResult Failed(
        DescriptorInputFailure failure,
        string message)
    {
        if (failure == DescriptorInputFailure.None)
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

        return new DescriptorInputParseResult(
            false,
            null,
            failure,
            message.Trim());
    }
}
