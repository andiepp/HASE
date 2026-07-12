namespace Hase.Runtime.Execution;

/// <summary>
/// Represents the result of an execution operation that returns a value.
/// </summary>
public sealed record ExecutionResult<T>
    : ExecutionResult
{
    public ExecutionResult(
        bool success,
        T value)
        : base(success)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the value returned by the execution operation.
    /// </summary>
    public T Value { get; }
}
