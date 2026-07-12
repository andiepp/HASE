namespace Hase.Runtime.Execution;

/// <summary>
/// Represents the result of an execution operation that does not
/// return a value.
/// </summary>
public record ExecutionResult
{
    public ExecutionResult(bool success)
    {
        Success = success;
    }

    /// <summary>
    /// Gets whether the execution operation completed successfully.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets a successful execution result.
    /// </summary>
    public static ExecutionResult Successful { get; } =
        new(true);

    /// <summary>
    /// Gets an unsuccessful execution result.
    /// </summary>
    public static ExecutionResult Failed { get; } =
        new(false);
}
