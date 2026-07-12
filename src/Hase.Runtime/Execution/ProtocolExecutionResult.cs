namespace Hase.Runtime.Execution;

/// <summary>
/// Represents the result of an execution operation independently
/// of the wire protocol.
/// </summary>
public sealed record ProtocolExecutionResult<T>(
    bool Success,
    T Value);