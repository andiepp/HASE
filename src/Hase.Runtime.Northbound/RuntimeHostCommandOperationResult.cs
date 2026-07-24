namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents the normalized result of a runtime-host Command execution.
/// </summary>
public sealed record RuntimeHostCommandOperationResult
{
    private RuntimeHostCommandOperationResult(
        RuntimeHostCommandOperationStatus status,
        object? returnValue,
        string? diagnostic)
    {
        Status =
            status;

        ReturnValue =
            returnValue;

        Diagnostic =
            string.IsNullOrWhiteSpace(
                diagnostic)
                ? null
                : diagnostic.Trim();
    }

    /// <summary>
    /// Gets the normalized Command execution status.
    /// </summary>
    public RuntimeHostCommandOperationStatus Status
    {
        get;
    }

    /// <summary>
    /// Gets whether the Command completed successfully.
    /// </summary>
    public bool IsSuccess =>
        Status
        == RuntimeHostCommandOperationStatus.Success;

    /// <summary>
    /// Gets the optional endpoint-provided return value after success.
    /// </summary>
    public object? ReturnValue
    {
        get;
    }

    /// <summary>
    /// Gets optional safe diagnostic text. Applications must not parse this
    /// text for program logic.
    /// </summary>
    public string? Diagnostic
    {
        get;
    }

    /// <summary>
    /// Creates a successful Command result.
    /// </summary>
    public static RuntimeHostCommandOperationResult Successful(
        object? returnValue = null)
    {
        return new RuntimeHostCommandOperationResult(
            RuntimeHostCommandOperationStatus.Success,
            returnValue,
            diagnostic: null);
    }

    /// <summary>
    /// Creates a failed Command result.
    /// </summary>
    public static RuntimeHostCommandOperationResult Failed(
        RuntimeHostCommandOperationStatus status,
        string? diagnostic = null)
    {
        ValidateFailureStatus(
            status);

        return new RuntimeHostCommandOperationResult(
            status,
            returnValue: null,
            diagnostic);
    }

    private static void ValidateFailureStatus(
        RuntimeHostCommandOperationStatus status)
    {
        if (!Enum.IsDefined(
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The runtime-host Command operation status is not defined.");
        }

        if (status
            == RuntimeHostCommandOperationStatus.Success)
        {
            throw new ArgumentException(
                "A failed Command-operation result cannot have Success status.",
                nameof(status));
        }
    }
}